namespace Xanthos

open System

/// Shared by the real COM connection and deterministic failure-injection tests.
module internal EventRegistration =
    let private failure api (ex: exn) =
        { Api = api
          Code = Some ex.HResult
          Kind = JvErrorKind.Invocation
          Outputs = Map.empty
          Message = ex.Message }

    let detachRemaining unregister registrations =
        let mutable firstError = None
        let failed = ResizeArray<_>()

        for (id, handler) in registrations do
            try
                unregister id handler
            with ex ->
                failed.Add(id, handler)

                if firstError.IsNone then
                    firstError <- Some(failure "ComEventsHelper.Remove" ex)

        let result =
            match firstError with
            | None -> Ok()
            | Some error ->
                Error
                    { error with
                        Outputs = Map.ofList [ "failedDispids", String.Join(",", failed |> Seq.map (fst >> string)) ] }

        List.ofSeq failed, result

    let detach unregister registrations =
        detachRemaining unregister registrations |> snd

    let attach register unregister registrations =
        let mutable registered = []

        try
            for (id, handler) in registrations do
                register id handler
                registered <- (id, handler) :: registered

            Ok()
        with ex ->
            let error = failure "ComEventsHelper.Combine" ex

            match detach unregister registered with
            | Ok() -> Error error
            | Error cleanup ->
                Error
                    { error with
                        Outputs = cleanup.Outputs.Add("cleanupError", cleanup.Message) }
