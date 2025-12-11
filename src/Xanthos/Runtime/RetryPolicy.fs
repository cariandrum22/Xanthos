namespace Xanthos.Runtime

open System
open Xanthos.Interop

/// <summary>
/// Represents the action to take when `JVRead` returns a non-payload outcome.
/// </summary>
type ReadRetryDecision =
    | RetryAfter of TimeSpan
    | Skip
    | Abort

module ReadRetryPolicy =

    /// <summary>
    /// Default retry policy for `JVRead` results emitted by JV-Link.
    /// </summary>
    let defaultPolicy outcome =
        match outcome with
        | JvReadOutcome.DownloadPending -> RetryAfter(TimeSpan.FromMilliseconds 500.)
        | JvReadOutcome.FileBoundary -> Skip
        | _ -> Abort
