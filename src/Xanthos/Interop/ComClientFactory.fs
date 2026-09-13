namespace Xanthos.Interop

open System
open Xanthos.Core

module ComClientFactory =
    let internal tryCreateWithActivator (activate: unit -> IJvLinkClient) : Result<IJvLinkClient, ComFault> =
        try
            Ok(activate ())
        with
        | ComActivationException fault -> Error fault
        | ex ->
            Error
                { Reason = ComFaultReason.ActivationFailure
                  Details = $"JV-Link activation failed in a {IntPtr.Size * 8}-bit process: {ex.Message}"
                  Exception = Some ex }

    /// <summary>
    /// Attempts to create a COM-backed JV-Link client.
    /// </summary>
    /// <param name="useJvGets">Optional flag to use JVGets API instead of JVRead.
    /// When None, falls back to environment variables (XANTHOS_USE_JVREAD opt-out, XANTHOS_USE_JVGETS legacy).</param>
    /// <returns>
    /// Ok with the client on success, or Error with details on failure.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Windows resolves the COM registration matching the current process architecture.
    /// Install the x64 JV-Link server for an x64 process, or x86 for an x86 process.
    /// </para>
    /// <para>
    /// Activation failures are returned explicitly; this function never creates a stub.
    /// </para>
    /// </remarks>
    let tryCreate (useJvGets: bool option) : Result<IJvLinkClient, ComFault> =
#if WINDOWS
        let progId =
            match Environment.GetEnvironmentVariable "XANTHOS_COM_PROGID" with
            | null
            | "" -> "JVDTLab.JVLink"
            | value -> value

        tryCreateWithActivator (fun () ->
            match useJvGets with
            | Some value -> new ComJvLinkClient(useJvGets = value, progId = progId) :> IJvLinkClient
            | None -> new ComJvLinkClient(progId = progId) :> IJvLinkClient)
#else
        let details =
            if OperatingSystem.IsWindows() then
                "COM interop is available only in the net10.0-windows build."
                + " Reference the Windows target and install JV-Link matching your process architecture."
            else
                "Non-Windows platform"

        Error
            { Reason = ComFaultReason.ActivationFailure
              Details = details
              Exception = None }
#endif

    /// <summary>
    /// Checks if COM is available by attempting to create and immediately dispose a client.
    /// </summary>
    /// <remarks>
    /// This function properly releases the COM reference after checking availability
    /// to prevent RCW (Runtime Callable Wrapper) leaks.
    /// </remarks>
    let isComAvailable () =
        match tryCreate None with
        | Ok c ->
            c.Dispose()
            true
        | Error _ -> false
