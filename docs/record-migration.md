# Migrating record parsing

Use `Xanthos.Records` for JV-Data responses. Its curried functions return `Result<_, RecordParseError>`; no COM session is needed for parsing.

```fsharp
open Xanthos

let raceDistance =
    Records.parseRA >> Result.map (fun race -> race.Distance.Value)

let title bytes =
    Records.parse bytes
    |> Result.map (function
        | Records.Record.RA race -> Some race.Name.Title
        | _ -> None)
```

All 38 official record IDs have complete models, individual parsers and cases in `Records.Record`. This includes breeding, offspring, entry statistics, training, schedules, mining and WH horse weight. Unknown IDs, including H5, return `Record.Unknown(id, raw)`.

## Source values and units

Every successful record retains a copy of its complete `Raw` bytes, including reserved areas and CRLF. `Sourced<'a>` keeps both the exact field text and its interpreted value. Text is not trimmed; Shift-JIS positions refer to bytes. Blank numeric fields generally become `None`, while zero remains `Some 0`; documented special meanings use unions. Codes preserve unfamiliar values through `OfficialCode`.

Money properties ending in `HundredYen` retain that unit; `AmountYen` and WIN5 carryover amounts are yen. Vote totals named `UnitsHundredYen` count 100-yen units. Weight and elapsed-time properties explicitly name kilograms or seconds. Odds distinguish no votes, missing quotes, cancellation before/after sale, ordinary quotes and upper-limit displays.

SE sectional times use `SectionalTime`: `999` means `NoResult`, initial zero or blanks mean `NotRecorded`, and measured values become `Seconds`. The raw text remains available, including the distinction between initial zero and spaces.

Header categories retain the SDK code. Deletion category `0` is accepted only where the specification defines it. Fields absent for a category retain their initial representation; consumers must check the category before treating a zero or blank as an observed result. Announcements supply month/day/hour/minute without a year or time zone; no year or UTC conversion is invented.

## Selecting historical formats

The default decodes expanded identifiers and current odds limits. Select historical interpretation explicitly from the acquisition context:

```fsharp
open Xanthos.Data

let parseLegacyIdentifiers =
    Records.parseWith
        { Records.ParseOptions.Default with
            IdentifierFormat = IdentifierFormat.Legacy }

let parseOldOdds = Records.parseO1WithFormat OddsLimitFormat.Before20040814
```

Historical identifier support retains shorter identifiers and original source error coordinates:

| Record | Legacy bytes | Expanded bytes |
|---|---:|---:|
| UM | 1577 | 1609 |
| BR | 537 | 545 |
| HN | 245 | 251 |
| SK | 178 | 208 |
| CK | 6864 | 6870 |
| HS | 196 | 200 |
| BT | 6887 | 6889 |

Select the format from the acquisition contract: DIFF/BLOD/SNAP/HOSE/TCOV/RCOV use legacy identifiers; DIFN/BLDN/SNPN/HOSN/TCVN/RCVN use expanded identifiers. The new streams contain deliveries from 2023-08-08, including records about earlier races. Passing the wrong format produces an error instead of inferring a layout solely from length. Historical behavior is verified against specification-based fixtures; real legacy samples have not yet been obtained.

Odds limits changed when trifecta sales began on 2004-08-14. Choose `Before20040814` when decoding the preceding odds convention; the selected convention remains on the result. The general dispatcher accepts both options through `Records.parseWith`.

`DataSpecs.parseOptions dataspec raceDate` selects both conventions and returns `None` for an unknown dataspec. `DataSpecs.tryFind` exposes expected normal/setup record sets and allowed options. Treat these sets as the documented baseline; preserve unrecognized records when the service adds new types.

For concatenated dataspecs, use `DataSpecs.parseOptionsForRecord dataspec recordId raceDate`. It selects identifier width from the streams that can supply that record and selects odds limits from the record's race date. Ambiguous legacy/expanded sources for the same record return an error; `validateOpen` also rejects those combinations before acquisition. Unknown record IDs remain preservable, while unknown dataspecs or an unexplained identifier layout return errors.

Call the optional pure `DataSpecs.validateOpen request` before acquisition to check dataspec/option combinations and time bounds. The interval is `(FromTime, ToTime]`, measured by delivery timestamp in Japanese service time. TOKU, DIFF/DIFN, HOSE/HOSN, HOYU and COMM do not allow an end time: the SDK returns NoData for that request. A NoData response to such a request does not demonstrate that the archive is empty. Low-level `JvLink.openData` retains native return codes without applying this optional preflight.

## Training, mining and entry statistics

HC/WC totals and laps are ordered from furthest away to the finish. Zero means measurement failure; WC all-nines means at least the display limit. HC has no documented upper-limit marker. DM converts `mssSS` to seconds and exposes faster/slower error margins separately. TM scores retain the 0.0–100.0 range. WH distinguishes missing weights, withdrawals, measurement failures and kilograms.

CK supplies race-entry results for the horse and its connections. Its jockey/trainer blocks use `EntryCareerPerformance`, whose distance bins and numeric widths differ from the master-record `CareerPerformance`. Finish-count arrays contain first through fifth place, then lower placings. The two performance slots contain current year, then lifetime.

## Existing callers

| Previous usage | Replacement |
|---|---|
| `Xanthos.Core.Records.RA.parse` | `Records.parseRA` |
| `RaceKey` | `Identity.Raw` |
| RA `RaceName` | `Name.Title` and the other race-name fields |
| TK as one horse | `Horses` plus `RegisteredCount` |
| WF as horse weight | WF is WIN5; horse weight has ID WH |
| O2 as wide odds | O2 is quinella; O3 is wide |
| BR/BN as breeding/offspring records | BR is breeder; BN is owner |

`Runtime.PayloadParser` and `JvLinkService.ParsePayload` now use the canonical parser. Their existing `TKRecord`, `RARecord`, etc. case names carry the new complete models. `parsePayloadWith` accepts explicit parsing options. A `RecordError` preserves the original `RecordParseError` within the runtime error union.

The compatibility service's original parsing methods use expanded identifiers and current odds limits. For legacy streams or odds from before 2004-08-14, supply `Records.ParseOptions` to `JvLinkService.ParsePayloadWith`, `ParsePayloadsWith` or `TryParsePayloadsWith`. Acquisition has corresponding `FetchTypedRecordsWith(request, options)` and `FetchTypedRecordsCollectErrorsWith(request, options)` methods. The latter retains successfully parsed records alongside failed payloads. `PayloadParser.parsePayloadsWith` and `tryParsePayloadsWith` expose the same choices as curried functions. Select options from the dataspec and the record's race date; split acquisitions with different format conventions before applying a single options value.

Previous parsing behavior is isolated under `Xanthos.Legacy.Records` for migration reference. Those models contain known specification mismatches and are not used by default. Tests importing that namespace verify old compatibility behavior; they do not establish official record compliance.
