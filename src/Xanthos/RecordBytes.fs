namespace Xanthos

open System
open System.Globalization
open System.Text

/// Positions are one-based byte offsets in the original record.
type RecordParseError =
    { RecordId: string
      Field: string
      Position: int
      Length: int
      Raw: byte[]
      Message: string }

/// Header codes are preserved; data-category meanings depend on the record type.
type RecordHeader =
    { RecordId: string
      DataCategory: string
      CreatedDateRaw: string
      CreatedDate: DateOnly option }

/// Lossless envelope. Reading a header does not certify the remaining record layout.
type RecordEnvelope = { Header: RecordHeader; Raw: byte[] }

/// Strict, byte-oriented primitives shared by official record readers.
[<RequireQualifiedAccess>]
module RecordBytes =
    let private encoding =
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
        Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)

    let private error recordId field position length raw message =
        Error
            { RecordId = recordId
              Field = field
              Position = position
              Length = length
              Raw = raw
              Message = message }

    let field recordId name position length (data: byte[]) =
        if
            isNull data
            || position < 1
            || length < 0
            || position - 1 > data.Length
            || length > data.Length - (position - 1)
        then
            error recordId name position length [||] "Field lies outside the supplied byte buffer."
        else
            Ok(data.AsSpan(position - 1, length).ToArray())

    /// Decode exactly one field; do not trim or normalize Japanese or ASCII spaces.
    let text recordId name position length data =
        field recordId name position length data
        |> Result.bind (fun bytes ->
            try
                Ok(encoding.GetString bytes)
            with :? DecoderFallbackException ->
                error recordId name position length bytes "Invalid Shift-JIS byte sequence.")

    let ascii recordId name position length data =
        field recordId name position length data
        |> Result.bind (fun bytes ->
            if bytes |> Array.exists (fun b -> b < 0x20uy || b > 0x7Euy) then
                error recordId name position length bytes "Expected printable ASCII bytes."
            else
                Ok(Encoding.ASCII.GetString bytes))

    /// Only blanks mean missing here. Zero remains a numeric value; signs require explicit permission.
    let number allowSign scale recordId name position length data =
        ascii recordId name position length data
        |> Result.bind (fun raw ->
            if raw |> Seq.forall ((=) ' ') then
                Ok None
            else
                let digits =
                    if allowSign && raw.Length > 0 && (raw[0] = '+' || raw[0] = '-') then
                        raw.Substring 1
                    else
                        raw

                let valid =
                    digits.Length > 0 && digits |> Seq.forall (fun c -> c >= '0' && c <= '9')

                match valid, Decimal.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) with
                | true, (true, value) when scale > 0M -> Ok(Some(value / scale))
                | _ ->
                    error recordId name position length (Encoding.ASCII.GetBytes raw) "Invalid numeric value or scale.")

    /// All-zero dates are retained as missing; malformed nonzero dates are errors.
    let date recordId name position data =
        ascii recordId name position 8 data
        |> Result.bind (fun raw ->
            if raw = "00000000" then
                Ok(raw, None)
            else
                match DateOnly.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None) with
                | true, value -> Ok(raw, Some value)
                | _ -> error recordId name position 8 (Encoding.ASCII.GetBytes raw) "Invalid yyyyMMdd date.")

    /// Reads the common eleven-byte prefix, independently of record version or payload length.
    let header data =
        ascii "" "RecordId" 1 2 data
        |> Result.bind (fun id ->
            if
                id
                |> Seq.exists (fun c -> not ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
            then
                error id "RecordId" 1 2 (Encoding.ASCII.GetBytes id) "Expected two uppercase ASCII letters or digits."
            else
                ascii id "DataCategory" 3 1 data
                |> Result.bind (fun category ->
                    date id "CreatedDate" 4 data
                    |> Result.map (fun (raw, date) ->
                        { RecordId = id
                          DataCategory = category
                          CreatedDateRaw = raw
                          CreatedDate = date })))

    /// Preserves an unknown record too; record-specific readers validate length/version separately.
    let envelope data =
        header data
        |> Result.map (fun header ->
            { Header = header
              Raw = Array.copy data })

    let crlf recordId position data =
        field recordId "CRLF" position 2 data
        |> Result.bind (fun bytes ->
            if bytes = [| 13uy; 10uy |] then
                Ok()
            else
                error recordId "CRLF" position 2 bytes "Expected CRLF at the layout's final two bytes.")
