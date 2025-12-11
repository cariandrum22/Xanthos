namespace Xanthos.Runtime

open System
open Xanthos.Core
open Xanthos.Core.Errors
open Xanthos.Core.Records
open Xanthos.Core.Records.RecordParser
open Xanthos.Core.Records.RecordTypes
open Xanthos.Interop

/// Discriminated union representing all supported parsed record types
type ParsedRecord =
    | TKRecord of TK.TKRecord
    | RARecord of RA.RARecord
    | SERecord of SE.SERecord
    | HRRecord of HR.HRRecord
    | O1Record of O1.O1Record
    | O2Record of O2.O2Record
    | O3Record of O3.O3Record
    | O4Record of O4.O4Record
    | O5Record of O5.O5Record
    | O6Record of O6.O6Record
    | H1Record of H1.H1Record
    | H5Record of H5.H5Record
    | H6Record of H6.H6Record
    | WFRecord of WF.WFRecord
    | JCRecord of JC.JCRecord
    | TCRecord of TC.TCRecord
    | CCRecord of CC.CCRecord
    | WERecord of WE.WERecord
    | AVRecord of AV.AVRecord
    | UMRecord of UM.UMRecord
    | KSRecord of KS.KSRecord
    | CHRecord of CH.CHRecord
    | BRRecord of BR.BRRecord
    | BNRecord of BN.BNRecord
    | RCRecord of RC.RCRecord
    | JGRecord of JG.JGRecord
    | WHRecord of WH.WHRecord
    | HNRecord of HN.HNRecord
    | SKRecord of SK.SKRecord
    | HCRecord of HC.HCRecord
    | HSRecord of HS.HSRecord
    | HYRecord of HY.HYRecord
    | YSRecord of YS.YSRecord
    | BTRecord of BT.BTRecord
    | CSRecord of CS.CSRecord
    | DMRecord of DM.DMRecord
    | TMRecord of TM.TMRecord
    | WCRecord of WC.WCRecord
    | CKRecord of CK.CKRecord
    | UnknownRecord of typeId: string * data: byte[]

/// Utility for parsing JvPayload into strongly typed domain records
module PayloadParser =

    /// Extracts the 2-character record type identifier from payload data
    let getRecordTypeId (data: byte[]) : string =
        if data.Length < 2 then
            ""
        else
            Text.decodeShiftJis data.[0..1]

    /// Parses a JvPayload into a strongly typed domain record
    let parsePayload (payload: JvPayload) : Result<ParsedRecord, XanthosError> =
        let data = payload.Data

        if data.Length < 2 then
            Error(ValidationError "Payload too short to contain record type identifier")
        else
            let typeId = getRecordTypeId data
            let recordType = RecordTypes.parse typeId

            match recordType with
            // Race data (implemented parsers)
            | RecordType.TK -> TK.parse data |> Result.map TKRecord
            | RecordType.RA -> RA.parse data |> Result.map RARecord
            | RecordType.SE -> SE.parse data |> Result.map SERecord
            | RecordType.HR -> HR.parse data |> Result.map HRRecord
            // Odds data (implemented parsers)
            | RecordType.O1 -> O1.parse data |> Result.map O1Record
            | RecordType.O2 -> O2.parse data |> Result.map O2Record
            | RecordType.O3 -> O3.parse data |> Result.map O3Record
            | RecordType.O4 -> O4.parse data |> Result.map O4Record
            | RecordType.O5 -> O5.parse data |> Result.map O5Record
            | RecordType.O6 -> O6.parse data |> Result.map O6Record
            // Vote count data (implemented parsers)
            | RecordType.H1 -> H1.parse data |> Result.map H1Record
            | RecordType.H5 -> H5.parse data |> Result.map H5Record
            | RecordType.H6 -> H6.parse data |> Result.map H6Record
            // Master data (implemented parsers)
            | RecordType.UM -> UM.parse data |> Result.map UMRecord
            | RecordType.KS -> KS.parse data |> Result.map KSRecord
            | RecordType.CH -> CH.parse data |> Result.map CHRecord
            | RecordType.BR -> BR.parse data |> Result.map BRRecord
            | RecordType.BN -> BN.parse data |> Result.map BNRecord
            | RecordType.RC -> RC.parse data |> Result.map RCRecord
            // Analysis/Real-time data (implemented parsers)
            | RecordType.WF -> WF.parse data |> Result.map WFRecord
            | RecordType.JC -> JC.parse data |> Result.map JCRecord
            | RecordType.TC -> TC.parse data |> Result.map TCRecord
            | RecordType.CC -> CC.parse data |> Result.map CCRecord
            | RecordType.WE -> WE.parse data |> Result.map WERecord
            | RecordType.AV -> AV.parse data |> Result.map AVRecord
            // Implemented real-time/exclusion data parsers
            | RecordType.JG -> JG.parse data |> Result.map JGRecord
            | RecordType.WH -> WH.parse data |> Result.map WHRecord
            | RecordType.HN -> HN.parse data |> Result.map HNRecord
            | RecordType.SK -> SK.parse data |> Result.map SKRecord
            | RecordType.HC -> HC.parse data |> Result.map HCRecord
            | RecordType.HS -> HS.parse data |> Result.map HSRecord
            | RecordType.HY -> HY.parse data |> Result.map HYRecord
            | RecordType.YS -> YS.parse data |> Result.map YSRecord
            | RecordType.BT -> BT.parse data |> Result.map BTRecord
            | RecordType.CS -> CS.parse data |> Result.map CSRecord
            | RecordType.DM -> DM.parse data |> Result.map DMRecord
            | RecordType.TM -> TM.parse data |> Result.map TMRecord
            | RecordType.WC -> WC.parse data |> Result.map WCRecord
            // Comprehensive race stats
            | RecordType.CK -> CK.parse data |> Result.map CKRecord
            // Truly unknown record types
            | RecordType.Unknown s -> Ok(UnknownRecord(s, data))

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
    let getTKRecords (records: ParsedRecord list) : TK.TKRecord list =
        records
        |> List.choose (function
            | TKRecord r -> Some r
            | _ -> None)

    /// Extract RA records from parsed records
    let getRARecords (records: ParsedRecord list) : RA.RARecord list =
        records
        |> List.choose (function
            | RARecord r -> Some r
            | _ -> None)

    /// Extract SE records from parsed records
    let getSERecords (records: ParsedRecord list) : SE.SERecord list =
        records
        |> List.choose (function
            | SERecord r -> Some r
            | _ -> None)

    /// Extract HR records from parsed records
    let getHRRecords (records: ParsedRecord list) : HR.HRRecord list =
        records
        |> List.choose (function
            | HRRecord r -> Some r
            | _ -> None)

    /// Extract O1 (Win Odds) records from parsed records
    let getO1Records (records: ParsedRecord list) : O1.O1Record list =
        records
        |> List.choose (function
            | O1Record r -> Some r
            | _ -> None)

    /// Extract WF (Horse Weight) records from parsed records
    let getWFRecords (records: ParsedRecord list) : WF.WFRecord list =
        records
        |> List.choose (function
            | WFRecord r -> Some r
            | _ -> None)

    /// Extract JC (Jockey Change) records from parsed records
    let getJCRecords (records: ParsedRecord list) : JC.JCRecord list =
        records
        |> List.choose (function
            | JCRecord r -> Some r
            | _ -> None)
