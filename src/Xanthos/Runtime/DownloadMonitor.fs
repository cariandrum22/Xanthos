namespace Xanthos.Runtime

open System
open System.Threading
open Xanthos.Core
open Xanthos.Core.Errors
open Xanthos.Interop

/// <summary>
/// Result of waiting for download completion.
/// </summary>
type WaitCompletionResult =
    /// <summary>Download completed successfully with the given file count.</summary>
    | Completed of completedCount: int
    /// <summary>Timeout reached before completion. Contains partial count at timeout.</summary>
    | TimedOut of partialCount: int

/// <summary>
/// Utilities for monitoring JV-Link download progress.
/// </summary>
/// <remarks>
/// JV-Link downloads data asynchronously. After calling JVOpen, use JVStatus to poll
/// for completion. This module provides a helper to wait for downloads to finish
/// with timeout and cancellation support.
/// </remarks>
module DownloadMonitor =

    /// <summary>
    /// Polls JV-Link via JVStatus until the reported completed file count reaches the expected value,
    /// or cancellation/timeout occurs.
    /// </summary>
    /// <param name="statusCall">Function that calls JVStatus and returns the completed count.</param>
    /// <param name="expectedDownloadCount">Number of files expected to be downloaded.</param>
    /// <param name="pollIntervalOpt">Time between status polls. Default: 500ms.</param>
    /// <param name="timeoutOpt">Maximum time to wait. None = wait indefinitely.</param>
    /// <param name="cancellationTokenOpt">Token to cancel the wait operation.</param>
    /// <returns>
    /// Ok(Completed count) on success, Ok(TimedOut partialCount) on timeout, or Error if statusCall fails.
    /// Throws OperationCanceledException if cancellation is requested.
    /// </returns>
    let waitForCompletion
        (statusCall: unit -> Result<int, ComError>)
        (expectedDownloadCount: int)
        (pollIntervalOpt: TimeSpan option)
        (timeoutOpt: TimeSpan option)
        (cancellationTokenOpt: CancellationToken option)
        : Result<WaitCompletionResult, ComError> =
        let interval = defaultArg pollIntervalOpt (TimeSpan.FromMilliseconds 500.)
        let ct = defaultArg cancellationTokenOpt CancellationToken.None

        let deadline =
            match timeoutOpt with
            | Some t -> Some(DateTime.UtcNow + t)
            | None -> None

        // Sleep that respects cancellation by sleeping in small chunks
        let sleepWithCancellation (span: TimeSpan) (token: CancellationToken) =
            let chunkMs = 50
            let mutable remaining = int span.TotalMilliseconds

            while remaining > 0 && not token.IsCancellationRequested do
                let sleepTime = min remaining chunkMs
                Thread.Sleep sleepTime
                remaining <- remaining - sleepTime

        let rec loop last =
            ct.ThrowIfCancellationRequested()

            match statusCall () with
            | Error e -> Error e
            | Ok completed when completed >= expectedDownloadCount -> Ok(Completed completed)
            | Ok completed ->
                match deadline with
                | Some d when DateTime.UtcNow >= d -> Ok(TimedOut completed)
                | _ ->
                    sleepWithCancellation interval ct
                    // Check cancellation again after sleep
                    ct.ThrowIfCancellationRequested()
                    loop completed

        loop 0
