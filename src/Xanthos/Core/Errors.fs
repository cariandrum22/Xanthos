namespace Xanthos.Core

open System

/// Describes recoverable and unrecoverable conditions originating from the JV-Link COM layer.
type ComError =
    | NotInitialized
    | InvalidInput of details: string
    | InvalidState of details: string
    | RegistryInvalid of details: string
    | CommunicationFailure of errorCode: int * details: string
    | Unexpected of details: string

/// Describes unrecoverable failures that happen before COM calls can be issued.
type ComFaultReason =
    | ActivationFailure
    | MethodResolutionFailure
    | InvocationFailure

type ComFault =
    { Reason: ComFaultReason
      Details: string
      Exception: exn option }

/// Library-wide error representation used by higher-level workflows.
type XanthosError =
    | InteropError of ComError
    | ValidationError of message: string
    | DataNotFound of key: string
    | Unsupported of feature: string
    | IOError of details: string
    | UnexpectedError of details: string
    /// Indicates the operation was cancelled via CancellationToken.
    /// This case ensures that all API methods return Result<'T, XanthosError>
    /// rather than allowing OperationCanceledException to escape.
    | Cancelled
    /// Indicates that the event queue reached its capacity and events were dropped.
    /// Subscribers can use this to detect backpressure and potential event loss.
    | EventQueueOverflow of droppedCount: int

module Errors =

    /// Lifts a `Result<'a, ComError>` into the higher-level `XanthosError` space.
    [<System.Obsolete("Use mapComError instead.")>]
    let mapComResult (result: Result<'a, ComError>) = Result.mapError InteropError result

    /// Alias: same as `mapComResult` for readability when mapping COM errors.
    let mapComError (result: Result<'a, ComError>) = Result.mapError InteropError result

    /// Helper to construct a validation error in a readable form.
    let validation message = ValidationError message

    /// Helper to construct an unexpected error.
    let unexpected message = UnexpectedError message

    /// Converts an error into a concise string suitable for logs.
    let toString =
        function
        | InteropError NotInitialized -> "JV-Link session is not initialised."
        | InteropError(InvalidInput details) -> $"Invalid input passed to JV-Link: {details}"
        | InteropError(InvalidState details) -> $"JV-Link reported an invalid state: {details}"
        | InteropError(RegistryInvalid details) -> $"JV-Link registry contents invalid: {details}"
        | InteropError(CommunicationFailure(code, details)) -> $"JV-Link communication failure (code={code}): {details}"
        | InteropError(Unexpected details) -> $"Unexpected JV-Link error: {details}"
        | ValidationError message -> $"Validation error: {message}"
        | DataNotFound key -> $"Data not found: {key}"
        | Unsupported feature -> $"Unsupported operation: {feature}"
        | IOError details -> $"File I/O error: {details}"
        | UnexpectedError details -> $"Error: {details}"
        | Cancelled -> "Operation was cancelled."
        | EventQueueOverflow droppedCount ->
            $"Event queue overflow: {droppedCount} event(s) dropped due to backpressure."
