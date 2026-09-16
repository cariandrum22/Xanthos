# Reading JV-Data without losing source values

`Codes.parse` accepts a `CodeTable` and its exact ASCII representation. It returns an `OfficialCode` for both recognized and unknown values; `Codes.isKnown`, `Codes.label` and `Codes.raw` distinguish them. Incorrect width or non-ASCII input returns `Error`.

```fsharp
Codes.parse CodeTable.Grade "A" |> Result.map Codes.label
// Ok (Some "G1（平地競走）")
Codes.parse CodeTable.Margin " 12" |> Result.map Codes.raw
// Ok " 12"
```

Do not trim codes or convert them to integers. Grade `" "`, coat `"01"` and margin `"1  "` have significant spaces or leading zeros. All 19 official tables are represented. Unknown values retain their table and raw code so applications can handle later extensions.

`RecordBytes.envelope` reads the common eleven-byte header and copies the original bytes. `RecordHeader` retains the record ID, data-category code and raw creation date. Category meanings are record-specific; an all-zero creation date becomes `None` with its raw representation retained. Invalid dates produce `RecordParseError`, including the record, field, byte position and offending value.

`RecordBytes.field`, `text`, `ascii`, `number`, `date` and `crlf` are curried primitives with one-based byte positions. Text decoding preserves Japanese, full-width spaces and ASCII spaces. Numbers accept a sign only when requested, distinguish blank from zero, and apply an explicit decimal scale.

Text uses Windows CP932, including its documented duplicate mappings such as `FA4A` → `Ⅰ` and `ED40` → `纊`. These decode identically on portable and Windows targets. Undefined or truncated sequences still return a located `RecordParseError`; decoding does not guess another encoding or replace malformed text. `RecordBytes.field`, parse-error bytes and complete record `Raw` retain the original byte spelling even when two CP932 encodings represent the same Unicode character. See the [Microsoft CP932 mapping](https://www.unicode.org/Public/MAPPINGS/VENDORS/MICSFT/WINDOWS/CP932.TXT).

`RecordKinds.ofId` supplies the official meaning, including O2 as quinella and O3 as wide. Unrecognized IDs, including unofficial H5, remain `Unknown`. Header recognition alone does not validate a record body or select a historical layout. See [record migration](record-migration.md) for supported bodies and explicit historical selection. The previous parsers are isolated under `Xanthos.Legacy.Records` and are not the runtime default.
