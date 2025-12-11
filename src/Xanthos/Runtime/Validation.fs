namespace Xanthos.Runtime

open System
open System.Globalization
open Xanthos.Core.Errors
open Xanthos.Interop

[<AutoOpen>]
module private ResultOps =
    let bind f =
        function
        | Ok value -> f value
        | Error err -> Error err

module Validation =

    /// <summary>
    /// Normalises and validates the dataspec string (must be a multiple of 4 alphanumeric characters).
    /// </summary>
    let normalizeDataspec (spec: string) =
        let normalized = spec.Trim().ToUpperInvariant()

        if String.IsNullOrWhiteSpace normalized then
            Error(validation "Dataspec must be a non-empty 4-character block (e.g. RACE).")
        elif normalized.Length % 4 <> 0 then
            Error(
                validation $"Dataspec '{spec}' must be composed of 4-character blocks (length must be a multiple of 4)."
            )
        elif normalized |> Seq.exists (fun ch -> not (Char.IsLetterOrDigit ch)) then
            Error(validation $"Dataspec '{spec}' contains invalid characters. Use only letters or digits.")
        else
            Ok normalized

    /// <summary>
    /// Parses the optional JVOpen option value, defaulting to <c>1</c> when unspecified.
    /// </summary>
    let parseOpenOption (text: string option) =
        match text with
        | None -> Ok 1
        | Some value ->
            match Int32.TryParse(value.Trim()) with
            | true, parsed when parsed >= 1 && parsed <= 4 -> Ok parsed
            | true, _ -> Error(validation $"JVOpen option '{value}' must be between 1 and 4.")
            | _ -> Error(validation $"JVOpen option '{value}' is not a valid integer.")

    /// <summary>
    /// Parses the required JVOpen fromTime (yyyyMMddHHmmss or yyyyMMdd).
    /// </summary>
    let parseFromTime (text: string option) =
        let formats = [| "yyyyMMddHHmmss"; "yyyyMMdd" |]

        match text with
        | None -> Error(validation "fromTime (--from or XANTHOS_JVLINK_FROM) is required for JVOpen.")
        | Some raw ->
            let trimmed = raw.Trim()
            let mutable parsed = DateTime.MinValue

            if
                DateTime.TryParseExact(
                    trimmed,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    &parsed
                )
            then
                Ok parsed
            else
                Error(
                    validation
                        $"Unable to parse --from / XANTHOS_JVLINK_FROM value '{raw}'. Expected yyyyMMddHHmmss or yyyyMMdd."
                )

    /// <summary>
    /// Constructs a <see cref="JvOpenRequest"/> from validated inputs.
    /// </summary>
    let createOpenRequest spec fromTime option =
        { Spec = spec
          FromTime = fromTime
          Option = option }

    /// <summary>
    /// Validates and builds a <see cref="JvOpenRequest"/> from raw CLI parameters.
    /// </summary>
    let buildOpenRequest rawSpec rawFrom rawOption =
        normalizeDataspec rawSpec
        |> bind (fun spec ->
            parseFromTime rawFrom
            |> bind (fun fromTime ->
                parseOpenOption rawOption
                |> bind (fun option -> Ok(createOpenRequest spec fromTime option))))

    /// <summary>
    /// Validates and normalizes a realtime API key for JVRTOpen.
    /// </summary>
    /// <param name="key">Raw key string from caller</param>
    /// <returns>Normalized key or ValidationError</returns>
    /// <remarks>
    /// Per JVRTOpen specification, valid key formats are:
    /// <list type="bullet">
    /// <item>YYYYMMDDJJKKHHRR (16 digits): Race-by-race request</item>
    /// <item>YYYYMMDDJJRR (12 digits): Simplified race-by-race request</item>
    /// <item>YYYYMMDD (8 digits): Daily request</item>
    /// <item>WatchEvent key (starts with "0B"): Event-triggered request</item>
    /// </list>
    /// All digits must be half-width ASCII (0-9).
    /// </remarks>
    let normalizeRealtimeKey (key: string) =
        if String.IsNullOrWhiteSpace key then
            Error(validation "Realtime key must be a non-empty string.")
        else
            // Normalize: convert full-width digits to half-width, trim whitespace
            let normalized = Xanthos.Core.Text.normalizeJvText key

            if String.IsNullOrWhiteSpace normalized then
                Error(validation "Realtime key must be a non-empty string after normalization.")
            else if
                // Check if it's a WatchEvent key (starts with "0B")
                normalized.StartsWith("0B", StringComparison.OrdinalIgnoreCase)
            then
                // WatchEvent keys are allowed through with minimal validation
                // They have variable format: 0B12RA20240101010112...
                Ok normalized
            else
                // For non-WatchEvent keys, validate digit-only format
                let isAllDigits = normalized |> Seq.forall Char.IsDigit

                if not isAllDigits then
                    Error(
                        validation
                            $"Realtime key '{key}' contains non-digit characters. Expected YYYYMMDDJJKKHHRR, YYYYMMDDJJRR, YYYYMMDD, or WatchEvent key."
                    )
                else
                    // Validate length: 8 (daily), 12 (race short), or 16 (race full)
                    match normalized.Length with
                    | 8 ->
                        // Daily format: YYYYMMDD - validate as date
                        let mutable parsed = DateTime.MinValue

                        if
                            DateTime.TryParseExact(
                                normalized,
                                "yyyyMMdd",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                &parsed
                            )
                        then
                            Ok normalized
                        else
                            Error(validation $"Realtime key '{key}' is not a valid date (YYYYMMDD).")
                    | 12 ->
                        // Simplified race format: YYYYMMDDJJRR - validate date portion
                        let datePart = normalized.Substring(0, 8)
                        let mutable parsed = DateTime.MinValue

                        if
                            DateTime.TryParseExact(
                                datePart,
                                "yyyyMMdd",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                &parsed
                            )
                        then
                            Ok normalized
                        else
                            Error(validation $"Realtime key '{key}' has an invalid date portion (YYYYMMDD).")
                    | 16 ->
                        // Full race format: YYYYMMDDJJKKHHRR - validate date portion
                        let datePart = normalized.Substring(0, 8)
                        let mutable parsed = DateTime.MinValue

                        if
                            DateTime.TryParseExact(
                                datePart,
                                "yyyyMMdd",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                &parsed
                            )
                        then
                            Ok normalized
                        else
                            Error(validation $"Realtime key '{key}' has an invalid date portion (YYYYMMDD).")
                    | len ->
                        Error(
                            validation
                                $"Realtime key '{key}' has invalid length ({len}). Expected 8, 12, or 16 digits, or a WatchEvent key."
                        )
