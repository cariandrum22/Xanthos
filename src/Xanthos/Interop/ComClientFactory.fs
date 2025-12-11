namespace Xanthos.Interop

open System
open Xanthos.Core

module ComClientFactory =
    /// <summary>
    /// Attempts to create a COM-backed JV-Link client.
    /// </summary>
    /// <param name="useJvGets">Optional flag to use JVGets API instead of JVRead.
    /// When None, falls back to XANTHOS_USE_JVGETS environment variable.</param>
    /// <returns>
    /// Ok with the client on success, or Error with details on failure.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Important:</b> JV-Link COM server only runs in 32-bit (x86) processes.
    /// Attempting to create a client from a 64-bit process will fail with
    /// REGDB_E_CLASSNOTREG because the COM server is not registered for 64-bit.
    /// </para>
    /// <para>
    /// Ensure your application targets x86 or AnyCPU with "Prefer 32-bit" enabled.
    /// </para>
    /// </remarks>
    let tryCreate (useJvGets: bool option) : Result<IJvLinkClient, ComFault> =
#if WINDOWS
        // JV-Link COM server is x86 only - fail early with clear error message
        if Environment.Is64BitProcess then
            Error
                { Reason = ComFaultReason.ActivationFailure
                  Details =
                    "JV-Link COM server requires a 32-bit (x86) process. "
                    + "The current process is 64-bit. Configure your application to target x86 or "
                    + "use AnyCPU with 'Prefer 32-bit' enabled in project settings."
                  Exception = None }
        else
            try
                let client =
                    match useJvGets with
                    | Some value -> new ComJvLinkClient(useJvGets = value) :> IJvLinkClient
                    | None -> new ComJvLinkClient() :> IJvLinkClient

                Ok client
            with
            | ComActivationException fault -> Error fault
            | ex ->
                Error
                    { Reason = ComFaultReason.ActivationFailure
                      Details = ex.Message
                      Exception = Some ex }
#else
        Error
            { Reason = ComFaultReason.ActivationFailure
              Details = "Non-Windows platform"
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
            c.Close()
            // Properly dispose the COM client if it implements IDisposable
            match box c with
            | :? IDisposable as d -> d.Dispose()
            | _ -> ()

            true
        | Error _ -> false
