namespace Xanthos.Interop

open Xanthos

/// Tracks successful native operations on the owning STA. Initialization alone does not open data.
type internal NativeSessionLifetime() =
    let mutable dataOpen = false
    let mutable watching = false

    member _.Watching = watching

    member _.Observe(api, code) =
        match api, code with
        | ("JVOpen" | "JVRTOpen"), (0 | -1 | -2)
        | "JVMVOpen", (0 | -1) -> dataOpen <- true
        | "JVClose", 0 -> dataOpen <- false
        | "JVWatchEvent", 0 -> watching <- true
        | "JVWatchEventClose", 0 -> watching <- false
        | _ -> () // A failed operation must not erase ownership of an existing session.

    /// Always attempt reference release, while retaining the first cleanup failure.
    member this.Cleanup(invoke: string -> Result<int, JvError>, detach: unit -> unit, release: unit -> unit) =
        let mutable firstError = None

        let remember error =
            if firstError.IsNone then
                firstError <- Some error

        let attempt api operation =
            try
                operation ()
            with
            | SessionCleanupException error -> remember error
            | ex ->
                let rec unwrap (error: exn) =
                    match error with
                    | :? System.Reflection.TargetInvocationException when not (isNull error.InnerException) ->
                        unwrap error.InnerException
                    | _ -> error

                let cause = unwrap ex

                remember
                    { Api = api
                      Code = Some cause.HResult
                      Kind = JvErrorKind.Invocation
                      Outputs = Map.empty
                      Message = cause.Message }

        let close api =
            attempt api (fun () ->
                match invoke api with
                | Ok 0 -> this.Observe(api, 0)
                | Ok code ->
                    remember
                        { Api = api
                          Code = Some code
                          Kind = JvErrorKind.Sdk
                          Outputs = Map.empty
                          Message = $"{api} returned SDK code {code} during cleanup." }
                | Error error -> remember error)

        if watching then
            close "JVWatchEventClose"

        attempt "disconnectEvents" detach

        if dataOpen then
            close "JVClose"

        attempt "FinalReleaseComObject" release

        match firstError with
        | Some error -> Error error
        | None -> Ok()
