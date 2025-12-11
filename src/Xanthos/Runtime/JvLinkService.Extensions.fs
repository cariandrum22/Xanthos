namespace Xanthos.Runtime

open System
open System.Threading
open Xanthos.Core
open Xanthos.Core.Errors

module ServiceExtensions =
    type JvLinkService with
        /// Waits until JV-Link reports all downloads completed (via JVStatus) or timeout/cancellation occurs.
        /// Returns Completed(count) on success, TimedOut(partialCount) if timeout is reached before completion.
        member this.WaitForDownloads
            (expectedCount: int, ?pollInterval: TimeSpan, ?timeout: TimeSpan, ?cancellationToken: CancellationToken)
            : Result<WaitCompletionResult, XanthosError> =
            let statusCall () =
                match this.GetStatus() with
                | Ok v -> Ok v
                | Error(InteropError(InvalidState _)) -> Ok 0
                | Error(InteropError e) -> Error e
                | Error other -> Error(Unexpected(sprintf "%A" other))

            DownloadMonitor.waitForCompletion statusCall expectedCount pollInterval timeout cancellationToken
            |> Result.mapError InteropError
