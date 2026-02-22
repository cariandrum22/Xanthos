namespace Xanthos.Runtime

open System
open Xanthos.Core.Errors

/// Configuration for COM call retry and timeout behavior.
/// Use this to tune timeout/retry values based on expected data volume and network conditions.
type ComRetryConfig =
    {
        /// Maximum time to wait for a single COM call before considering it hung.
        /// Default: 30 seconds. Increase for slow connections or large data volumes.
        Timeout: TimeSpan
        /// Number of retry attempts for transient failures (communication errors).
        /// Default: 2 for general calls, 0 for read operations (which have their own retry logic).
        MaxRetries: int
    }

    /// Default configuration: 30s timeout, 2 retries (suitable for most use cases).
    static member Default =
        { Timeout = TimeSpan.FromSeconds 30.
          MaxRetries = 2 }

    /// Configuration for high-volume operations: 60s timeout, 3 retries.
    static member HighVolume =
        { Timeout = TimeSpan.FromSeconds 60.
          MaxRetries = 3 }

    /// Configuration for read operations: uses timeout but no retries (readAll has its own retry logic).
    static member ReadOnly =
        { Timeout = TimeSpan.FromSeconds 30.
          MaxRetries = 0 }

/// Represents minimal configuration required to talk to the JV-Link runtime.
type JvLinkConfig =
    {
        Sid: string
        SavePath: string option
        ServiceKey: string option
        /// When Some true, use JVGets (byte array) instead of JVRead (BSTR).
        /// When None, falls back to environment variables (XANTHOS_USE_JVREAD opt-out, XANTHOS_USE_JVGETS legacy).
        UseJvGets: bool option
    }

module JvLinkConfig =

    /// Maximum byte length for SID (JVInit spec: 64 bytes max).
    [<Literal>]
    let private MaxSidBytes = 64

    /// Validates SID per JV-Link JVInit specification:
    /// - Non-empty
    /// - Max 64 bytes (Shift-JIS encoding)
    /// - Only printable ASCII characters allowed (0x21-0x7E)
    /// Note: Leading/trailing whitespace is trimmed before validation.
    let private validateSid (sid: string) =
        if String.IsNullOrWhiteSpace sid then
            Error(validation "SID must be a non-empty string.")
        else
            // Trim leading/trailing whitespace before validation
            let trimmed = sid.Trim()

            if String.IsNullOrEmpty trimmed then
                Error(validation "SID must be a non-empty string.")
            else
                // Check for invalid characters (only printable ASCII 0x21-0x7E allowed)
                let hasInvalidChar = trimmed |> Seq.exists (fun c -> c < '\u0021' || c > '\u007E')

                if hasInvalidChar then
                    Error(validation "SID contains invalid characters. Only printable ASCII (0x21-0x7E) is allowed.")
                else
                    // Check byte length (Shift-JIS encoding, but ASCII is 1 byte per char)
                    let byteLength = System.Text.Encoding.ASCII.GetByteCount(trimmed)

                    if byteLength > MaxSidBytes then
                        Error(validation $"SID exceeds maximum length of {MaxSidBytes} bytes.")
                    else
                        Ok trimmed

    /// <summary>
    /// Validates raw configuration values and produces a trimmed <see cref="JvLinkConfig"/>.
    /// </summary>
    /// <param name="sid">Software ID for JV-Link authentication</param>
    /// <param name="savePath">Optional path for JV-Link data storage</param>
    /// <param name="serviceKey">Optional service key for authentication</param>
    /// <param name="useJvGets">Optional flag to use JVGets API. None = fallback to env var</param>
    /// <remarks>
    /// SID validation per JV-Link JVInit specification:
    /// <list type="bullet">
    /// <item>Non-empty string</item>
    /// <item>Max 64 bytes</item>
    /// <item>No leading space</item>
    /// <item>Only printable ASCII characters (0x21-0x7E)</item>
    /// </list>
    /// </remarks>
    let create sid savePath serviceKey useJvGets =
        match validateSid sid with
        | Error err -> Error err
        | Ok validSid ->
            Ok
                { Sid = validSid
                  SavePath = savePath
                  ServiceKey = serviceKey
                  UseJvGets = useJvGets }
