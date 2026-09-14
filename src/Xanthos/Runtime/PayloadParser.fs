namespace Xanthos.Runtime

open Xanthos
open Xanthos.Core
open Xanthos.Core.Errors
open Xanthos.Interop

/// Runtime adapter over the canonical, lossless record projection.
type ParsedRecord =
    | TKRecord of Records.TK
    | RARecord of Records.RA
    | SERecord of Records.SE
    | HRRecord of Records.HR
    | H1Record of Records.H1
    | H6Record of Records.H6
    | O1Record of Records.O1
    | O2Record of Records.O2
    | O3Record of Records.O3
    | O4Record of Records.O4
    | O5Record of Records.O5
    | O6Record of Records.O6
    | UMRecord of Records.UM
    | KSRecord of Records.KS
    | CHRecord of Records.CH
    | BRRecord of Records.BR
    | BNRecord of Records.BN
    | RCRecord of Records.RC
    | WFRecord of Records.WF
    | WERecord of Records.WE
    | AVRecord of Records.AV
    | JCRecord of Records.JC
    | TCRecord of Records.TC
    | CCRecord of Records.CC
    | HNRecord of Records.HN
    | SKRecord of Records.SK
    | HSRecord of Records.HS
    | HYRecord of Records.HY
    | JGRecord of Records.JG
    | HCRecord of Records.HC
    | WCRecord of Records.WC
    | WHRecord of Records.WH
    | YSRecord of Records.YS
    | BTRecord of Records.BT
    | CSRecord of Records.CS
    | DMRecord of Records.DM
    | TMRecord of Records.TM
    | CKRecord of Records.CK
    | UnknownRecord of typeId: string * data: byte[]

module PayloadParser =
    let getRecordTypeId (data: byte[]) =
        RecordBytes.ascii "" "RecordId" 1 2 data |> Result.defaultValue ""

    let parsePayloadWith options (payload: JvPayload) : Result<ParsedRecord, XanthosError> =
        Records.parseWith options payload.Data
        |> Result.map (function
            | Records.Record.TK record -> TKRecord record
            | Records.Record.RA record -> RARecord record
            | Records.Record.SE record -> SERecord record
            | Records.Record.HR record -> HRRecord record
            | Records.Record.H1 record -> H1Record record
            | Records.Record.H6 record -> H6Record record
            | Records.Record.O1 record -> O1Record record
            | Records.Record.O2 record -> O2Record record
            | Records.Record.O3 record -> O3Record record
            | Records.Record.O4 record -> O4Record record
            | Records.Record.O5 record -> O5Record record
            | Records.Record.O6 record -> O6Record record
            | Records.Record.UM record -> UMRecord record
            | Records.Record.KS record -> KSRecord record
            | Records.Record.CH record -> CHRecord record
            | Records.Record.BR record -> BRRecord record
            | Records.Record.BN record -> BNRecord record
            | Records.Record.RC record -> RCRecord record
            | Records.Record.WF record -> WFRecord record
            | Records.Record.WE record -> WERecord record
            | Records.Record.AV record -> AVRecord record
            | Records.Record.JC record -> JCRecord record
            | Records.Record.TC record -> TCRecord record
            | Records.Record.CC record -> CCRecord record
            | Records.Record.HN record -> HNRecord record
            | Records.Record.SK record -> SKRecord record
            | Records.Record.HS record -> HSRecord record
            | Records.Record.HY record -> HYRecord record
            | Records.Record.JG record -> JGRecord record
            | Records.Record.HC record -> HCRecord record
            | Records.Record.WC record -> WCRecord record
            | Records.Record.WH record -> WHRecord record
            | Records.Record.YS record -> YSRecord record
            | Records.Record.BT record -> BTRecord record
            | Records.Record.CS record -> CSRecord record
            | Records.Record.DM record -> DMRecord record
            | Records.Record.TM record -> TMRecord record
            | Records.Record.CK record -> CKRecord record
            | Records.Record.Unknown(id, data) -> UnknownRecord(id, data))
        |> Result.mapError RecordError

    let parsePayload payload =
        parsePayloadWith Records.ParseOptions.Default payload

    /// Parses multiple payloads, returning all successfully parsed records.
    /// Fails fast on the first parse error without processing remaining payloads.
    let parsePayloads (payloads: JvPayload list) : Result<ParsedRecord list, XanthosError> =
        payloads
        |> List.fold
            (fun acc payload ->
                match acc with
                | Error e -> Error e // Already failed, skip remaining payloads
                | Ok records ->
                    match parsePayload payload with
                    | Ok record -> Ok(record :: records)
                    | Error e -> Error e)
            (Ok [])
        |> Result.map List.rev

    /// Parses payloads, collecting both successes and failures
    let tryParsePayloads (payloads: JvPayload list) : ParsedRecord list * (JvPayload * XanthosError) list =
        let mutable successes = []
        let mutable failures = []

        for payload in payloads do
            match parsePayload payload with
            | Ok record -> successes <- record :: successes
            | Error err -> failures <- (payload, err) :: failures

        (List.rev successes, List.rev failures)

    /// Filters parsed records by type
    let filterByType<'T> (records: ParsedRecord list) (extractor: ParsedRecord -> 'T option) : 'T list =
        records |> List.choose extractor

    /// Extract TK records from parsed records
    let getTKRecords (records: ParsedRecord list) : Xanthos.Records.TK list =
        records
        |> List.choose (function
            | TKRecord r -> Some r
            | _ -> None)

    /// Extract RA records from parsed records
    let getRARecords (records: ParsedRecord list) : Xanthos.Records.RA list =
        records
        |> List.choose (function
            | RARecord r -> Some r
            | _ -> None)

    /// Extract SE records from parsed records
    let getSERecords (records: ParsedRecord list) : Xanthos.Records.SE list =
        records
        |> List.choose (function
            | SERecord r -> Some r
            | _ -> None)

    /// Extract HR records from parsed records
    let getHRRecords (records: ParsedRecord list) : Xanthos.Records.HR list =
        records
        |> List.choose (function
            | HRRecord r -> Some r
            | _ -> None)

    /// Extract O1 (Win Odds) records from parsed records
    let getO1Records (records: ParsedRecord list) : Xanthos.Records.O1 list =
        records
        |> List.choose (function
            | O1Record r -> Some r
            | _ -> None)

    /// Extract WF (WIN5) records from parsed records
    let getWFRecords (records: ParsedRecord list) : Xanthos.Records.WF list =
        records
        |> List.choose (function
            | WFRecord r -> Some r
            | _ -> None)

    /// Extract JC (Jockey Change) records from parsed records
    let getJCRecords (records: ParsedRecord list) : Xanthos.Records.JC list =
        records
        |> List.choose (function
            | JCRecord r -> Some r
            | _ -> None)
