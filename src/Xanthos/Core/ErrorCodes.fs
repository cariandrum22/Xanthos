namespace Xanthos.Core

open System
open Xanthos.Core.Errors

/// Helper functions to translate JV-Link return codes into strongly typed errors.
module ErrorCodes =

    /// Custom error code for COM event connection failure (not from JV-Link itself).
    [<Literal>]
    let ComConnectionFailure = -9001

    let private successCodes = set [ 0 ]

    let private notInitializedCodes = set [ -201; -202; -203 ]
    let private registryInvalidCodes = set [ -211 ]

    let private formatOutput message methodName code documentation =
        let withDoc =
            if String.IsNullOrWhiteSpace documentation then
                message
            else
                $"{message} Detail: {documentation}"

        if String.IsNullOrWhiteSpace methodName then
            sprintf "%s (code %d)." withDoc code
        else
            sprintf "%s (code %d, method %s)." withDoc code methodName

    /// Converts a JV-Link return code into either success or an appropriate `ComError`.
    let interpret methodName (code: int) : Result<unit, ComError> =
        if successCodes.Contains code || code > 0 then
            Ok()
        else
            match ErrorCatalog.tryFind methodName code with
            | Some info ->
                let methodKey =
                    if String.IsNullOrWhiteSpace methodName then
                        "(UNKNOWN)"
                    else
                        methodName.ToUpperInvariant()

                let overrideInfo = info.Overrides |> Map.tryFind methodKey

                let category =
                    overrideInfo
                    |> Option.bind (fun o -> o.Category)
                    |> Option.defaultValue info.Base.Category

                let documentationText =
                    overrideInfo
                    |> Option.bind (fun o -> o.Documentation)
                    |> Option.defaultValue info.Base.Documentation

                let messageText =
                    overrideInfo
                    |> Option.bind (fun o -> o.Message)
                    |> Option.defaultValue info.Base.Message
                    |> fun msg -> formatOutput msg methodName code documentationText

                match category with
                | JvErrorCategory.Input -> Error(InvalidInput messageText)
                | JvErrorCategory.Authentication ->
                    if notInitializedCodes.Contains code then
                        Error NotInitialized
                    elif registryInvalidCodes.Contains code then
                        Error(RegistryInvalid messageText)
                    else
                        Error(InvalidState messageText)
                | JvErrorCategory.Maintenance -> Error(InvalidState messageText)
                | JvErrorCategory.Download -> Error(CommunicationFailure(code, messageText))
                | JvErrorCategory.Internal -> Error(Unexpected messageText)
                | JvErrorCategory.State ->
                    if notInitializedCodes.Contains code then
                        Error NotInitialized
                    elif registryInvalidCodes.Contains code then
                        Error(RegistryInvalid messageText)
                    else
                        Error(InvalidState messageText)
                | JvErrorCategory.Other -> Error(CommunicationFailure(code, messageText))
            | None -> Error(CommunicationFailure(code, $"Unhandled JV-Link return code {code} in {methodName}."))
