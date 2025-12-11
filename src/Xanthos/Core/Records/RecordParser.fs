/// <summary>
/// Record parsing infrastructure for JV-Link fixed-length data records.
/// </summary>
/// <remarks>
/// <para>
/// JV-Link returns data as fixed-length Shift-JIS encoded byte arrays. Each record type
/// has a specific layout defined by field specifications (FieldSpec). This module provides
/// the parsing infrastructure to extract typed values from these binary records.
/// </para>
/// <para>
/// <strong>Record Format:</strong> All JV-Link records start with a 2-byte record type
/// identifier (e.g., "RA" for race data, "UM" for horse data). Use <c>getRecordType</c>
/// to identify the record type before parsing.
/// </para>
/// <para>
/// <strong>Field Encodings:</strong> Fields use various encodings including:
/// <list type="bullet">
/// <item>Text: Shift-JIS encoded strings with fullwidth normalization</item>
/// <item>Integer: Decimal digits as ASCII/Shift-JIS text</item>
/// <item>Decimal: Fixed-point with implicit decimal places</item>
/// <item>Date: Various formats (yyyyMMdd, yyyyMMddHHmm, etc.)</item>
/// <item>Flag: "1" = true, other = false</item>
/// <item>Code: Lookup codes for code tables</item>
/// </list>
/// </para>
/// <para>
/// See JV-Data specification in docs/official/ for complete record layouts.
/// </para>
/// </remarks>
namespace Xanthos.Core.Records

open System
open System.Text
open Xanthos.Core
open Xanthos.Core.Text
open Xanthos.Core.Errors
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.CodeTables

/// <summary>
/// Core parsing functions for JV-Link fixed-length records.
/// </summary>
/// <remarks>
/// This module provides low-level parsing utilities used by specific record parsers
/// (RA.fs, UM.fs, etc.) to extract typed values from binary data.
/// </remarks>
module RecordParser =

    /// <summary>
    /// Represents a parsed field value with type information.
    /// </summary>
    /// <remarks>
    /// This is the simplified variant used for most parsing operations.
    /// Option types are used to represent missing/invalid values in numeric and date fields.
    /// </remarks>
    type FieldValue =
        /// <summary>Parsed text string.</summary>
        | TextValue of string
        /// <summary>Parsed integer, None if missing or invalid.</summary>
        | IntValue of int option
        /// <summary>Parsed decimal, None if missing or invalid.</summary>
        | DecimalValue of decimal option
        /// <summary>Parsed date, None if missing or invalid.</summary>
        | DateValue of DateTime option
        /// <summary>Boolean flag (true if field contains "1").</summary>
        | BoolValue of bool
        /// <summary>Raw byte array (unparsed).</summary>
        | BytesValue of byte[]
        /// <summary>Code table lookup value.</summary>
        | CodeValue of string

    /// <summary>
    /// Represents a parsed field value with explicit missing indicators.
    /// </summary>
    /// <remarks>
    /// This variant distinguishes between "value is 0" and "value is missing/blank",
    /// which is important for some JV-Data fields where zero has meaning.
    /// </remarks>
    type RichFieldValue =
        /// <summary>Parsed text string.</summary>
        | RText of string
        /// <summary>Parsed integer value.</summary>
        | RInt of int
        /// <summary>Integer field was blank or unparseable.</summary>
        | RIntMissing
        /// <summary>Parsed decimal value.</summary>
        | RDecimal of decimal
        /// <summary>Decimal field was blank or unparseable.</summary>
        | RDecimalMissing
        /// <summary>Parsed date value.</summary>
        | RDate of DateTime
        /// <summary>Date field was blank or all zeros.</summary>
        | RDateMissing
        /// <summary>Boolean flag value.</summary>
        | RBool of bool
        /// <summary>Raw byte array.</summary>
        | RBytes of byte[]
        /// <summary>Code table lookup value.</summary>
        | RCode of string

    /// <summary>
    /// Errors that can occur during record parsing.
    /// </summary>
    type RecordParseError =
        /// <summary>Record data is shorter than expected for the field.</summary>
        | RecordTooShort of expected: int * actual: int
        /// <summary>Field extraction failed (encoding error, etc.).</summary>
        | FieldExtractionFailed of fieldName: string * reason: string
        /// <summary>Field value is invalid for its expected type.</summary>
        | InvalidFieldValue of fieldName: string * value: string * reason: string
        /// <summary>Record type identifier not recognized.</summary>
        | UnknownRecordType of typeId: string
        /// <summary>Code table lookup returned no match.</summary>
        | CodeTableLookupFailed of table: string * value: string

    /// <summary>
    /// Converts a RecordParseError to a XanthosError for unified error handling.
    /// </summary>
    /// <param name="error">The record parse error to convert.</param>
    /// <returns>A XanthosError (ValidationError) with a descriptive message.</returns>
    let toXanthosError (error: RecordParseError) : XanthosError =
        match error with
        | RecordTooShort(expected, actual) ->
            ValidationError $"Record too short: expected {expected} bytes, got {actual}"
        | FieldExtractionFailed(fieldName, reason) -> ValidationError $"Failed to extract field '{fieldName}': {reason}"
        | InvalidFieldValue(fieldName, value, reason) ->
            ValidationError $"Invalid value for field '{fieldName}': '{value}' - {reason}"
        | UnknownRecordType typeId -> ValidationError $"Unknown record type: {typeId}"
        | CodeTableLookupFailed(table, value) ->
            ValidationError $"Code table '{table}' lookup failed for value '{value}'"

    /// <summary>
    /// Extracts a byte slice from record data at the specified offset and length.
    /// </summary>
    /// <param name="data">The source byte array.</param>
    /// <param name="offset">Zero-based starting position.</param>
    /// <param name="length">Number of bytes to extract.</param>
    /// <returns>Ok with the byte slice, or Error if the slice would exceed data bounds.</returns>
    let extractBytes (data: byte[]) (offset: int) (length: int) : Result<byte[], RecordParseError> =
        if offset + length > data.Length then
            Error(RecordTooShort(offset + length, data.Length))
        else
            Ok(data.[offset .. offset + length - 1])

    // Internal primitive parsers
    let private tryParseInt (bytes: byte[]) : int option =
        let text = decodeShiftJis bytes |> fun s -> s.Trim()

        if String.IsNullOrWhiteSpace text then
            None
        else
            match Int32.TryParse text with
            | true, v -> Some v
            | _ -> None

    let private tryParseDecimal (bytes: byte[]) (precision: int) : decimal option =
        let text = decodeShiftJis bytes |> fun s -> s.Trim()

        if String.IsNullOrWhiteSpace text then
            None
        else
            match Decimal.TryParse text with
            | true, v -> Some(v / pown 10M precision)
            | _ -> None

    let private tryParseDate (bytes: byte[]) (format: string) : DateTime option =
        let text = decodeShiftJis bytes |> fun s -> s.Trim()

        if String.IsNullOrWhiteSpace text || text = String('0', text.Length) then
            None
        else
            let mutable parsed = DateTime.MinValue

            if
                DateTime.TryParseExact(
                    text,
                    format,
                    Globalization.CultureInfo.InvariantCulture,
                    Globalization.DateTimeStyles.None,
                    &parsed
                )
            then
                Some parsed
            else
                None

    let private tryParseFlag (bytes: byte[]) : bool =
        decodeShiftJis bytes |> fun s -> s.Trim() = "1"

    /// <summary>Parses a byte array as an integer. Returns None if empty or invalid.</summary>
    let parseInt (bytes: byte[]) : int option = tryParseInt bytes

    /// <summary>Parses a byte array as a fixed-point decimal with the specified precision.</summary>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="precision">Number of decimal places (value is divided by 10^precision).</param>
    let parseDecimal (bytes: byte[]) (precision: int) : decimal option = tryParseDecimal bytes precision

    /// <summary>Parses a byte array as a date using the specified format string.</summary>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="format">DateTime format string (e.g., "yyyyMMdd").</param>
    let parseDate (bytes: byte[]) (format: string) : DateTime option = tryParseDate bytes format

    /// <summary>Parses a byte array as a boolean flag ("1" = true).</summary>
    let parseFlag (bytes: byte[]) : bool = tryParseFlag bytes

    /// <summary>
    /// Parses a single field from record data using a field specification.
    /// Returns a RichFieldValue that distinguishes between zero and missing values.
    /// </summary>
    /// <param name="data">The source record byte array.</param>
    /// <param name="spec">The field specification defining offset, length, and encoding.</param>
    /// <returns>Ok with the parsed RichFieldValue, or Error on extraction failure.</returns>
    let parseFieldRich (data: byte[]) (spec: FieldSpec) : Result<RichFieldValue, RecordParseError> =
        match extractBytes data spec.ByteOffset spec.ByteLength with
        | Error e -> Error e
        | Ok bytes ->
            try
                match spec.Encoding with
                | FieldEncoding.Text -> decodeShiftJis bytes |> normalizeJvText |> (fun v -> RText(v.Trim()) |> Ok)
                | FieldEncoding.TextRaw -> decodeShiftJis bytes |> fun v -> RText(v.Trim()) |> Ok
                | FieldEncoding.Integer ->
                    (match tryParseInt bytes with
                     | Some v -> RInt v
                     | None -> RIntMissing)
                    |> Ok
                | FieldEncoding.Decimal p ->
                    (match tryParseDecimal bytes p with
                     | Some v -> RDecimal v
                     | None -> RDecimalMissing)
                    |> Ok
                | FieldEncoding.Date f ->
                    (match tryParseDate bytes f with
                     | Some v -> RDate v
                     | None -> RDateMissing)
                    |> Ok
                | FieldEncoding.Flag -> RBool(tryParseFlag bytes) |> Ok
                | FieldEncoding.Bytes -> RBytes bytes |> Ok
                | FieldEncoding.Code _ -> decodeShiftJis bytes |> fun v -> RCode(v.Trim()) |> Ok
            with ex ->
                Error(FieldExtractionFailed(spec.Name, ex.Message))

    /// <summary>
    /// Parses a single field from record data using a field specification.
    /// Uses Option types to represent missing values.
    /// </summary>
    /// <param name="data">The source record byte array.</param>
    /// <param name="spec">The field specification defining offset, length, and encoding.</param>
    /// <returns>Ok with the parsed FieldValue, or Error on extraction failure.</returns>
    let parseField (data: byte[]) (spec: FieldSpec) : Result<FieldValue, RecordParseError> =
        parseFieldRich data spec
        |> Result.map (function
            | RText v -> TextValue v
            | RInt v -> IntValue(Some v)
            | RIntMissing -> IntValue None
            | RDecimal v -> DecimalValue(Some v)
            | RDecimalMissing -> DecimalValue None
            | RDate v -> DateValue(Some v)
            | RDateMissing -> DateValue None
            | RBool b -> BoolValue b
            | RBytes bs -> BytesValue bs
            | RCode c -> CodeValue c)

    /// <summary>
    /// Extracts the 2-byte record type identifier from record data.
    /// </summary>
    /// <param name="data">The source record byte array.</param>
    /// <returns>The record type string (e.g., "RA", "UM"), or empty string if data is too short.</returns>
    let getRecordType (data: byte[]) : string =
        if data.Length < 2 then "" else decodeShiftJis data.[0..1]

    /// <summary>Computation expression builder for Result with RecordParseError.</summary>
    type ParseBuilder() =
        member _.Bind(m, f) =
            match m with
            | Ok v -> f v
            | Error e -> Error e

        member _.Return v = Ok v
        member _.ReturnFrom m = m

    /// <summary>Computation expression instance for parsing with RecordParseError.</summary>
    let parse = ParseBuilder()

    /// <summary>Computation expression builder for Result with XanthosError.</summary>
    type XanthosParseBuilder() =
        member _.Bind(m: Result<'a, XanthosError>, f) = Result.bind f m
        member _.Return v = Ok v
        member _.ReturnFrom m = m

    /// <summary>Computation expression instance for parsing with XanthosError.</summary>
    let parseRecord = XanthosParseBuilder()

    /// <summary>
    /// Parses multiple fields from record data according to a list of specifications.
    /// </summary>
    /// <param name="data">The source record byte array.</param>
    /// <param name="specs">List of field specifications to parse.</param>
    /// <returns>Ok with a Map of field names to FieldValues, or Error on first parse failure.</returns>
    let parseFields (data: byte[]) (specs: FieldSpec list) : Result<Map<string, FieldValue>, RecordParseError> =
        specs
        |> List.fold
            (fun acc spec ->
                match acc with
                | Error _ -> acc
                | Ok m ->
                    match parseField data spec with
                    | Ok v -> Ok(Map.add spec.Name v m)
                    | Error e -> Error e)
            (Ok Map.empty)

    /// <summary>Parse fields returning XanthosError (for use with parseRecord builder).</summary>
    let parseFieldsXanthos (data: byte[]) (specs: FieldSpec list) : Result<Map<string, FieldValue>, XanthosError> =
        parseFields data specs |> Result.mapError toXanthosError

    /// <summary>
    /// Parses multiple fields from record data, returning RichFieldValue for each.
    /// </summary>
    /// <param name="data">The source record byte array.</param>
    /// <param name="specs">List of field specifications to parse.</param>
    /// <returns>Ok with a Map of field names to RichFieldValues, or Error on first parse failure.</returns>
    let parseFieldsRich (data: byte[]) (specs: FieldSpec list) : Result<Map<string, RichFieldValue>, RecordParseError> =
        specs
        |> List.fold
            (fun acc spec ->
                match acc with
                | Error _ -> acc
                | Ok m ->
                    match parseFieldRich data spec with
                    | Ok v -> Ok(Map.add spec.Name v m)
                    | Error e -> Error e)
            (Ok Map.empty)

    /// <summary>Gets an optional text field value from parsed fields.</summary>
    /// <param name="fields">Map of parsed field values.</param>
    /// <param name="name">Field name to retrieve.</param>
    /// <returns>Some text if present and non-empty, None otherwise.</returns>
    let getText (fields: Map<string, FieldValue>) (name: string) : string option =
        match fields.TryFind name with
        | Some(TextValue v) when not (String.IsNullOrWhiteSpace v) -> Some v
        | _ -> None

    /// <summary>Gets a required text field value, returning Error if missing or empty.</summary>
    /// <param name="fields">Map of parsed field values.</param>
    /// <param name="name">Field name to retrieve.</param>
    /// <returns>Ok with the text value, or Error if missing/empty.</returns>
    let getRequiredText (fields: Map<string, FieldValue>) (name: string) : Result<string, RecordParseError> =
        match getText fields name with
        | Some v -> Ok v
        | None -> Error(InvalidFieldValue(name, "", "Required field is missing or empty"))

    /// <summary>Gets an optional integer field value from parsed fields.</summary>
    /// <param name="fields">Map of parsed field values.</param>
    /// <param name="name">Field name to retrieve.</param>
    /// <returns>Some int if present and valid, None otherwise.</returns>
    let getInt (fields: Map<string, FieldValue>) (name: string) : int option =
        match fields.TryFind name with
        | Some(IntValue v) -> v
        | _ -> None

    /// <summary>Gets an optional decimal field value from parsed fields.</summary>
    /// <param name="fields">Map of parsed field values.</param>
    /// <param name="name">Field name to retrieve.</param>
    /// <returns>Some decimal if present and valid, None otherwise.</returns>
    let getDecimal (fields: Map<string, FieldValue>) (name: string) : decimal option =
        match fields.TryFind name with
        | Some(DecimalValue v) -> v
        | _ -> None

    /// <summary>Gets an optional date field value from parsed fields.</summary>
    /// <param name="fields">Map of parsed field values.</param>
    /// <param name="name">Field name to retrieve.</param>
    /// <returns>Some DateTime if present and valid, None otherwise.</returns>
    let getDate (fields: Map<string, FieldValue>) (name: string) : DateTime option =
        match fields.TryFind name with
        | Some(DateValue v) -> v
        | _ -> None

    /// <summary>Gets a boolean flag value from parsed fields (defaults to false).</summary>
    /// <param name="fields">Map of parsed field values.</param>
    /// <param name="name">Field name to retrieve.</param>
    /// <returns>The boolean value, or false if field not found.</returns>
    let getBool (fields: Map<string, FieldValue>) (name: string) : bool =
        match fields.TryFind name with
        | Some(BoolValue v) -> v
        | _ -> false

    /// <summary>Gets an optional code table value, parsed as an enum type.</summary>
    /// <param name="fields">Map of parsed field values.</param>
    /// <param name="name">Field name to retrieve.</param>
    /// <typeparam name="T">The enum type to parse the code as.</typeparam>
    /// <returns>Some enum value if present and valid, None otherwise.</returns>
    let getCode<'T when 'T: enum<int>> (fields: Map<string, FieldValue>) (name: string) : 'T option =
        match fields.TryFind name with
        | Some(CodeValue v) -> parseCode<'T> v
        | _ -> None

    /// <summary>Gets an optional raw bytes field value from parsed fields.</summary>
    /// <param name="fields">Map of parsed field values.</param>
    /// <param name="name">Field name to retrieve.</param>
    /// <returns>Some byte array if present, None otherwise.</returns>
    let getBytes (fields: Map<string, FieldValue>) (name: string) : byte[] option =
        match fields.TryFind name with
        | Some(BytesValue v) -> Some v
        | _ -> None

    /// <summary>Gets a required text field value, returning XanthosError if missing.</summary>
    /// <param name="fields">Map of parsed field values.</param>
    /// <param name="name">Field name to retrieve.</param>
    /// <returns>Ok with the text value, or Error(XanthosError) if missing/empty.</returns>
    let requireText (fields: Map<string, FieldValue>) (name: string) : Result<string, XanthosError> =
        getRequiredText fields name |> Result.mapError toXanthosError

    /// <summary>Gets a required integer field value, returning XanthosError if missing.</summary>
    /// <param name="fields">Map of parsed field values.</param>
    /// <param name="name">Field name to retrieve.</param>
    /// <returns>Ok with the int value, or Error(XanthosError) if missing/invalid.</returns>
    let requireInt (fields: Map<string, FieldValue>) (name: string) : Result<int, XanthosError> =
        match getInt fields name with
        | Some v -> Ok v
        | None -> Error(toXanthosError (InvalidFieldValue(name, "", "Required integer field is missing or invalid")))

    // --- RichFieldValue getters ---
    // These functions work with RichFieldValue maps and return None for missing fields
    // (unlike FieldValue which uses Option inside IntValue, etc.)

    /// <summary>Attempts to get a RichFieldValue by name from the fields map.</summary>
    let tryGetRich (name: string) (fields: Map<string, RichFieldValue>) = Map.tryFind name fields

    /// <summary>Gets a text value from RichFieldValue map.</summary>
    let getRichText name fields =
        match tryGetRich name fields with
        | Some(RText v) -> Some v
        | _ -> None

    /// <summary>Gets an integer value from RichFieldValue map (None for missing or RIntMissing).</summary>
    let getRichInt name fields =
        match tryGetRich name fields with
        | Some(RInt v) -> Some v
        | _ -> None

    /// <summary>Gets a decimal value from RichFieldValue map (None for missing or RDecimalMissing).</summary>
    let getRichDecimal name fields =
        match tryGetRich name fields with
        | Some(RDecimal v) -> Some v
        | _ -> None

    /// <summary>Gets a date value from RichFieldValue map (None for missing or RDateMissing).</summary>
    let getRichDate name fields =
        match tryGetRich name fields with
        | Some(RDate v) -> Some v
        | _ -> None

    /// <summary>Gets a boolean value from RichFieldValue map.</summary>
    let getRichBool name fields =
        match tryGetRich name fields with
        | Some(RBool b) -> Some b
        | _ -> None

    /// <summary>Gets raw bytes from RichFieldValue map.</summary>
    let getRichBytes name fields =
        match tryGetRich name fields with
        | Some(RBytes bs) -> Some bs
        | _ -> None

    /// <summary>Gets and parses a code value as an enum from RichFieldValue map.</summary>
    let getRichCode<'T when 'T: enum<int>> name fields =
        match tryGetRich name fields with
        | Some(RCode c) -> parseCode<'T> c
        | _ -> None
