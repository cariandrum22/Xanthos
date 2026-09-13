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
              DeliveryStart = if expanded then Some(DateOnly(2023, 8, 8)) else None })
        |> List.map (fun definition -> definition.Id, definition)
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

    /// Optional preflight. Low-level JvLink.openData still preserves the native return code.
    /// The interval is (FromTime, ToTime], in service-local Japanese time.
    let validateOpen (request: OpenRequest) =
        if String.IsNullOrEmpty request.Dataspec || request.Dataspec.Length % 4 <> 0 then
            Error "Dataspec must contain one or more four-character identifiers."
        elif request.ToTime |> Option.exists (fun ending -> ending < request.FromTime) then
            Error "End time precedes start time."
        else
            let blocks =
                [ for i in 0..4 .. request.Dataspec.Length - 1 -> request.Dataspec.Substring(i, 4) ]

            blocks
            |> List.fold
                (fun result id ->
                    result
                    |> Result.bind (fun () ->
                        match tryFind id with
                        | None -> Error $"Unknown dataspec: {id}."
                        | Some d when not (d.OpenOptions.Contains request.Option) ->
                            Error $"{id} does not support JVOpen option {request.Option}."
                        | Some d when request.ToTime.IsSome && not d.SupportsEndTime ->
                            Error $"{id} does not support an end time; the SDK returns NoData."
                        | Some _ -> Ok()))
                (Ok())
