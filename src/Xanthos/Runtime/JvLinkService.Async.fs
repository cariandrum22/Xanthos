namespace Xanthos.Runtime

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open Xanthos.Core
open Xanthos.Interop

/// <summary>
/// Async extension methods for JvLinkService.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Important:</strong> Most methods in this module (marked with [Obsolete]) are thin
/// wrappers around synchronous methods using <c>async { return ... }</c>. They do NOT offload
/// work to a background thread and will block the calling thread while COM operations execute.
/// </para>
/// <para>
/// These methods exist for API convenience but provide no true asynchrony benefit.
/// For responsive UIs, consider using <c>Task.Run</c> to offload the synchronous calls,
/// or use <c>StreamRealtimeAsync</c>/<c>StreamRealtimePayloadsCollectAsync</c> which are
/// genuinely asynchronous.
/// </para>
/// <para>
/// <strong>Why COM can't be truly async:</strong> JV-Link COM objects require a Single-Threaded
/// Apartment (STA) thread. All COM calls are dispatched to this dedicated thread, making true
/// async impractical without fundamentally changing the threading model.
/// </para>
/// </remarks>
module AsyncServiceExtensions =

    // Message for obsolete sync wrappers
    [<Literal>]
    let private SyncWrapperObsoleteMessage =
        "This method wraps a synchronous call and blocks the calling thread. "
        + "Consider using Task.Run() to offload to a background thread, or use the synchronous version directly."

    type JvLinkService with

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.FetchPayloadsAsync
            (request: JvOpenRequest, ?cancellationToken: CancellationToken)
            : Async<Result<JvPayload list, XanthosError>> =
            async { return this.FetchPayloads(request, ?cancellationToken = cancellationToken) }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.FetchPayloadsWithSizeAsync
            (request: JvOpenRequest, ?cancellationToken: CancellationToken)
            : Async<Result<JvPayload list * int64, XanthosError>> =
            async { return this.FetchPayloadsWithBytes(request, ?cancellationToken = cancellationToken) }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.GetStatusAsync() : Async<Result<int, XanthosError>> = async { return this.GetStatus() }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.SkipCurrentFileAsync() : Async<Result<unit, XanthosError>> = async { return this.SkipCurrentFile() }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.CancelDownloadAsync() : Async<Result<unit, XanthosError>> = async { return this.CancelDownload() }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.DeleteFileAsync(filename: string) : Async<Result<unit, XanthosError>> =
            async { return this.DeleteFile filename }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.CheckMovieAvailabilityAsync(key: string) : Async<Result<MovieAvailability, XanthosError>> =
            async { return this.CheckMovieAvailability key }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.CheckMovieAvailabilityAsync
            (movieType: MovieType, key: string)
            : Async<Result<MovieAvailability, XanthosError>> =
            async { return this.CheckMovieAvailability(movieType, key) }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.PlayMovieAsync(key: string) : Async<Result<unit, XanthosError>> =
            async { return this.PlayMovie key }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.PlayMovieAsync(movieType: MovieType, key: string) : Async<Result<unit, XanthosError>> =
            async { return this.PlayMovie(movieType, key) }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.GenerateSilksFileAsync
            (pattern: string, outputPath: string)
            : Async<Result<SilksImage, XanthosError>> =
            async { return this.GenerateSilksFile(pattern, outputPath) }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.GetSilksBinaryAsync(pattern: string) : Async<Result<SilksImage, XanthosError>> =
            async { return this.GetSilksBinary pattern }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.GetCourseDiagramAsync(key: string) : Async<Result<CourseDiagram, XanthosError>> =
            async { return this.GetCourseDiagram key }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.GetCourseDiagramBasicAsync
            (key: string, filepath: string)
            : Async<Result<CourseDiagram, XanthosError>> =
            async { return this.GetCourseDiagramBasic(key, filepath) }

        [<Obsolete(SyncWrapperObsoleteMessage)>]
        member this.FetchWorkoutVideosAsync
            (movieType: MovieType, searchKey: string)
            : Async<Result<WorkoutVideoListing list, XanthosError>> =
            async { return this.FetchWorkoutVideos(movieType, searchKey) }

        /// <summary>
        /// Collects payloads from a streaming request into a list asynchronously.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Unlike other *Async methods in this module, this method is genuinely asynchronous.
        /// It consumes the <see cref="StreamPayloadsAsync"/> IAsyncEnumerable and collects
        /// all payloads into a list, properly awaiting between reads.
        /// </para>
        /// <para>
        /// Use this as an alternative to <see cref="FetchPayloads"/> when you want async/await
        /// semantics but still need a complete list of results.
        /// </para>
        /// <para>
        /// <strong>Warning:</strong> This method accumulates ALL payloads in memory. For large
        /// datasets or realtime streams, memory usage grows unboundedly and may cause OutOfMemoryException.
        /// Use <see cref="StreamPayloadsAsync"/> for lazy enumeration instead.
        /// </para>
        /// </remarks>
        /// <param name="request">The JV-Link open request specifying dataspec and time range.</param>
        /// <param name="pollInterval">Optional interval between polls when waiting for downloads (default: 500ms)</param>
        /// <param name="cancellationToken">Optional cancellation token for cooperative cancellation</param>
        /// <returns>A list of all collected payloads, or the first error encountered</returns>
        [<Obsolete("This method accumulates all payloads in memory. For large datasets or realtime streams, use StreamPayloadsAsync for lazy enumeration to avoid unbounded memory growth.")>]
        member this.StreamPayloadsCollectAsync
            (request: JvOpenRequest, ?pollInterval: TimeSpan, ?cancellationToken: CancellationToken)
            : Async<Result<JvPayload list, XanthosError>> =
            async {
                let token = defaultArg cancellationToken CancellationToken.None

                let stream =
                    this.StreamPayloadsAsync(request, ?pollInterval = pollInterval, cancellationToken = token)

                let enumerator = stream.GetAsyncEnumerator(token)
                let results = System.Collections.Generic.List<JvPayload>()
                let mutable finished = false
                let mutable error: XanthosError option = None
                let mutable caughtException: exn option = None

                try
                    while not finished do
                        let! hasNext = enumerator.MoveNextAsync().AsTask() |> Async.AwaitTask

                        if hasNext then
                            match enumerator.Current with
                            | Ok payload -> results.Add payload
                            | Error e ->
                                error <- Some e
                                finished <- true
                        else
                            finished <- true
                with ex ->
                    caughtException <- Some ex

                // Dispose asynchronously to avoid deadlocks on UI threads or blocking SynchronizationContexts
                do! enumerator.DisposeAsync().AsTask() |> Async.AwaitTask

                // Re-throw original exception if any (preserving stack trace)
                match caughtException with
                | Some ex ->
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw()
                    return Unchecked.defaultof<_> // Never reached
                | None ->
                    match error with
                    | Some e -> return Error e
                    | None -> return Ok(List.ofSeq results)
            }

        /// <summary>
        /// Collects realtime payloads into a list asynchronously.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Unlike other *Async methods in this module, this method is genuinely asynchronous.
        /// It consumes the <see cref="StreamRealtimeAsync"/> IAsyncEnumerable and collects
        /// all payloads into a list, properly awaiting between polls.
        /// </para>
        /// <para>
        /// <strong>Warning:</strong> This method accumulates ALL payloads in memory. Since realtime
        /// streams continue indefinitely until cancelled, memory usage grows unboundedly and WILL
        /// cause OutOfMemoryException. Use <see cref="StreamRealtimeAsync"/> for lazy enumeration instead.
        /// </para>
        /// </remarks>
        /// <param name="spec">Data spec ID (4 characters, e.g., "0B12", "0B11", "0B16")</param>
        /// <param name="key">Request key: "YYYYMMDDJJKKHHRR" (race), "YYYYMMDD" (daily), or WatchEvent parameter</param>
        /// <param name="pollInterval">Optional interval between polls (default: 500ms)</param>
        /// <param name="cancellationToken">Optional cancellation token for cooperative cancellation</param>
        /// <returns>A list of all collected payloads, or the first error encountered</returns>
        [<Obsolete("Realtime streams continue indefinitely - this method WILL cause OutOfMemoryException. Use StreamRealtimeAsync for lazy enumeration instead.")>]
        member this.StreamRealtimePayloadsCollectAsync
            (spec: string, key: string, ?pollInterval: TimeSpan, ?cancellationToken: CancellationToken)
            : Async<Result<JvPayload list, XanthosError>> =
            async {
                let token = defaultArg cancellationToken CancellationToken.None

                let stream =
                    this.StreamRealtimeAsync(spec, key, ?pollInterval = pollInterval, cancellationToken = token)

                let enumerator = stream.GetAsyncEnumerator(token)
                let results = System.Collections.Generic.List<JvPayload>()
                let mutable finished = false
                let mutable error: XanthosError option = None
                let mutable caughtException: exn option = None

                try
                    while not finished do
                        let! hasNext = enumerator.MoveNextAsync().AsTask() |> Async.AwaitTask

                        if hasNext then
                            match enumerator.Current with
                            | Ok payload -> results.Add payload
                            | Error e ->
                                error <- Some e
                                finished <- true
                        else
                            finished <- true
                with ex ->
                    caughtException <- Some ex

                // Dispose asynchronously to avoid deadlocks on UI threads or blocking SynchronizationContexts
                do! enumerator.DisposeAsync().AsTask() |> Async.AwaitTask

                // Re-throw original exception if any (preserving stack trace)
                match caughtException with
                | Some ex ->
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw()
                    return Unchecked.defaultof<_> // Never reached
                | None ->
                    match error with
                    | Some e -> return Error e
                    | None -> return Ok(List.ofSeq results)
            }
