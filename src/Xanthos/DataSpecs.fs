namespace Xanthos

open System

/// Pure acquisition metadata from the SDK's data-type list and JVOpen contract.
[<RequireQualifiedAccess>]
module DataSpecs =
    type Definition =
        {
            Id: string
            RecordIds: Set<string>
            SetupRecordIds: Set<string>
            OpenOptions: Set<int>
            IsRealtime: bool
            SupportsEndTime: bool
            IdentifierFormat: Data.IdentifierFormat
            /// Expanded streams contain deliveries from this date, even for older races.
            DeliveryStart: DateOnly option
        }

    let private definitions =
        [ "TOKU", "TK"
          "RACE", "RA SE HR H1 H6 O1 O2 O3 O4 O5 O6 WF JG"
          "DIFF", "UM KS CH BR BN RC RA SE"
          "DIFN", "UM KS CH BR BN RC RA SE"
          "BLOD", "HN SK BT"
          "BLDN", "HN SK BT"
          "MING", "DM TM"
          "SNAP", "CK"
          "SNPN", "CK"
          "SLOP", "HC"
          "WOOD", "WC"
          "YSCH", "YS"
          "HOSE", "HS"
          "HOSN", "HS"
          "HOYU", "HY"
          "COMM", "CS"
          "TCOV", "UM CH BR BN RC RA SE"
          "TCVN", "UM CH BR BN RC RA SE"
          "RCOV", "UM KS CH BR BN RC RA SE"
          "RCVN", "UM KS CH BR BN RC RA SE"
          "0B12", "RA SE HR"
          "0B15", "RA SE HR"
          "0B30", "O1 O2 O3 O4 O5 O6"
          "0B31", "O1"
          "0B32", "O2"
          "0B33", "O3"
          "0B34", "O4"
          "0B35", "O5"
          "0B36", "O6"
          "0B20", "H1 H6"
          "0B11", "WH"
          "0B14", "WE AV JC TC CC"
          "0B16", "WE AV JC TC CC"
          "0B13", "DM"
          "0B17", "TM"
          "0B41", "O1"
          "0B42", "O2"
          "0B51", "WF" ]
        |> List.map (fun (id, records) ->
            let realtime = id.StartsWith("0B", StringComparison.Ordinal)
            let supplementation = List.contains id [ "TCOV"; "TCVN"; "RCOV"; "RCVN" ]
            let expanded = List.contains id [ "DIFN"; "BLDN"; "SNPN"; "HOSN"; "TCVN"; "RCVN" ]
            let legacy = List.contains id [ "DIFF"; "BLOD"; "SNAP"; "HOSE"; "TCOV"; "RCOV" ]
            let recordIds = records.Split(' ') |> Set.ofArray

            let options =
                if realtime then
                    Set.empty
                elif supplementation then
                    Set.singleton 2
                elif List.contains id [ "TOKU"; "RACE"; "SNAP"; "SNPN" ] then
                    Set.ofList [ 1; 2; 3; 4 ]
                else
                    Set.ofList [ 1; 3; 4 ]

            let definition =
                { Id = id
                  RecordIds = recordIds
                  SetupRecordIds =
                    if id = "DIFF" || id = "DIFN" then
                        recordIds - Set.ofList [ "RA"; "SE" ]
                    elif realtime || supplementation then
                        Set.empty
                    else
                        recordIds
                  OpenOptions = options
                  IsRealtime = realtime
                  SupportsEndTime =
                    not realtime
                    && not (List.contains id [ "TOKU"; "DIFF"; "DIFN"; "HOSE"; "HOSN"; "HOYU"; "COMM" ])
                  IdentifierFormat =
                    if legacy then
                        Data.IdentifierFormat.Legacy
                    else
                        Data.IdentifierFormat.Expanded
                  DeliveryStart = if expanded then Some(DateOnly(2023, 8, 8)) else None }

            id, definition)
        |> Map.ofList

    let all = definitions |> Map.toList |> List.map snd
    let tryFind id = Map.tryFind id definitions

    /// Race date controls historical odds limits; delivery date does not select identifier width.
    let parseOptions id (raceDate: DateOnly) =
        tryFind id
        |> Option.map (fun definition ->
            { IdentifierFormat = definition.IdentifierFormat
              OddsLimitFormat =
                if raceDate < DateOnly(2004, 8, 14) then
                    Data.OddsLimitFormat.Before20040814
                else
                    Data.OddsLimitFormat.Current }
            : Records.ParseOptions)

    let private identifierRecords =
        Set.ofList [ "UM"; "BR"; "HN"; "SK"; "CK"; "HS"; "BT" ]

    let private resolveBlocks (dataspec: string) =
        if String.IsNullOrEmpty dataspec || dataspec.Length % 4 <> 0 then
            Error "Dataspec must contain one or more four-character identifiers."
        else
            [ for i in 0..4 .. dataspec.Length - 1 -> dataspec.Substring(i, 4) ]
            |> List.fold
                (fun result id ->
                    result
                    |> Result.bind (fun found ->
                        match tryFind id with
                        | Some definition -> Ok(definition :: found)
                        | None -> Error $"Unknown dataspec: {id}."))
                (Ok [])

    let private formatsFor recordId definitions =
        definitions
        |> List.filter (fun definition ->
            definition.RecordIds.Contains recordId
            || definition.SetupRecordIds.Contains recordId)
        |> List.map _.IdentifierFormat
        |> List.distinct

    /// Resolves concatenated dataspecs using record identity, never by guessing from byte length.
    /// Shared legacy/expanded layouts are ambiguous and must be acquired separately.
    let parseOptionsForRecord dataspec recordId (raceDate: DateOnly) =
        resolveBlocks dataspec
        |> Result.bind (fun definitions ->
            let format =
                if not (identifierRecords.Contains recordId) then
                    Ok Data.IdentifierFormat.Expanded
                else
                    match formatsFor recordId definitions with
                    | [ format ] -> Ok format
                    | [] -> Error $"No identifier format is defined for {recordId} in {dataspec}."
                    | _ ->
                        Error
                            $"Ambiguous identifier formats for {recordId} in {dataspec}; acquire the streams separately."

            format
            |> Result.map (fun identifierFormat ->
                { IdentifierFormat = identifierFormat
                  OddsLimitFormat =
                    if raceDate < DateOnly(2004, 8, 14) then
                        Data.OddsLimitFormat.Before20040814
                    else
                        Data.OddsLimitFormat.Current }
                : Records.ParseOptions))

    /// Optional preflight. Low-level JvLink.openData still preserves the native return code.
    /// The interval is (FromTime, ToTime], in service-local Japanese time.
    let validateOpen (request: OpenRequest) =
        if String.IsNullOrEmpty request.Dataspec || request.Dataspec.Length % 4 <> 0 then
            Error "Dataspec must contain one or more four-character identifiers."
        elif request.ToTime |> Option.exists (fun ending -> ending < request.FromTime) then
            Error "End time precedes start time."
        else
            resolveBlocks request.Dataspec
            |> Result.bind (fun definitions ->
                match
                    identifierRecords
                    |> Seq.tryFind (fun id -> (formatsFor id definitions).Length > 1)
                with
                | Some id ->
                    Error
                        $"Ambiguous identifier formats for {id} in {request.Dataspec}; acquire the streams separately."
                | None ->
                    definitions
                    |> List.fold
                        (fun result d ->
                            result
                            |> Result.bind (fun () ->
                                if not (d.OpenOptions.Contains request.Option) then
                                    Error $"{d.Id} does not support JVOpen option {request.Option}."
                                elif request.ToTime.IsSome && not d.SupportsEndTime then
                                    Error $"{d.Id} does not support an end time; the SDK returns NoData."
                                else
                                    Ok()))
                        (Ok()))
