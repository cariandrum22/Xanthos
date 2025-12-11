namespace Xanthos.Core.Records

/// <summary>
/// Field definition DSL for parsing fixed-length JV-Data records.
/// </summary>
/// <remarks>
/// <para>
/// JV-Link returns data as fixed-length byte arrays where each field occupies
/// a specific byte range. This module provides a DSL to define field specifications
/// declaratively.
/// </para>
/// <para>
/// <strong>Usage Example:</strong>
/// <code>
/// let raFields = [
///     text "RecordType" 0 2       // Bytes 0-1: "RA"
///     date "RaceDate" 2 8 "yyyyMMdd"
///     int "RaceNumber" 10 2
///     text "RaceName" 12 60
/// ]
/// </code>
/// </para>
/// </remarks>
module FieldDefinitions =

    /// <summary>
    /// Represents how a field value should be encoded/decoded.
    /// </summary>
    type FieldEncoding =
        /// <summary>Shift-JIS text with fullwidth→ASCII normalization.</summary>
        | Text
        /// <summary>Shift-JIS text without normalization.</summary>
        | TextRaw
        /// <summary>Numeric string parsed as int.</summary>
        | Integer
        /// <summary>Numeric string parsed as fixed-point decimal with given precision.</summary>
        | Decimal of precision: int
        /// <summary>Date string parsed using the specified format.</summary>
        | Date of format: string
        /// <summary>Code value for lookup in a code table.</summary>
        | Code of codeTable: string
        /// <summary>Boolean flag ("1" = true, other = false).</summary>
        | Flag
        /// <summary>Raw bytes (unparsed).</summary>
        | Bytes

    /// <summary>
    /// Represents a field position in a fixed-length record.
    /// </summary>
    type FieldSpec =
        {
            /// <summary>Field name used as key in parsed results.</summary>
            Name: string
            /// <summary>Zero-based byte offset in the record.</summary>
            ByteOffset: int
            /// <summary>Number of bytes this field occupies.</summary>
            ByteLength: int
            /// <summary>How to decode the bytes.</summary>
            Encoding: FieldEncoding
        }

    /// Define a text field (with normalization)
    let text name offset length =
        { Name = name
          ByteOffset = offset
          ByteLength = length
          Encoding = Text }

    /// Define a raw text field (without normalization)
    let textRaw name offset length =
        { Name = name
          ByteOffset = offset
          ByteLength = length
          Encoding = TextRaw }

    /// Define an integer field
    let int name offset length =
        { Name = name
          ByteOffset = offset
          ByteLength = length
          Encoding = Integer }

    /// Define a decimal field with precision
    let decimal name offset length precision =
        { Name = name
          ByteOffset = offset
          ByteLength = length
          Encoding = Decimal precision }

    /// Define a date field
    let date name offset length format =
        { Name = name
          ByteOffset = offset
          ByteLength = length
          Encoding = Date format }

    /// Define a code table field
    let code name offset length table =
        { Name = name
          ByteOffset = offset
          ByteLength = length
          Encoding = Code table }

    /// Define a flag field
    let flag name offset length =
        { Name = name
          ByteOffset = offset
          ByteLength = length
          Encoding = Flag }

    /// Define a raw bytes field
    let bytes name offset length =
        { Name = name
          ByteOffset = offset
          ByteLength = length
          Encoding = Bytes }
