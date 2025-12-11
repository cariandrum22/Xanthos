namespace Xanthos.Interop

open System
open System.Collections.Concurrent
open System.Runtime.ExceptionServices
open System.Threading
open System.Threading.Tasks

/// Generic dispatcher contract used to execute work on a dedicated STA thread.
type IComDispatcher =
    inherit IDisposable
    abstract member Invoke<'T> : name: string * call: (unit -> 'T) -> 'T
    abstract member InvokeAsync<'T> : name: string * call: (unit -> 'T) -> Task<'T>

/// Implemented by types that can expose their underlying COM dispatcher instance.
type IComDispatchProvider =
    abstract member Dispatcher: IComDispatcher

#if WINDOWS

open System.Runtime.InteropServices

/// Internal P/Invoke helpers for COM initialisation and message loop.
module private StaNative =

    [<Literal>]
    let COINIT_APARTMENTTHREADED = 0u

    [<Literal>]
    let WM_QUIT = 0x0012u

    [<Literal>]
    let WM_USER = 0x0400u

    /// Custom message to signal work is available
    [<Literal>]
    let WM_EXECUTE_WORK = WM_USER + 1u

    [<Literal>]
    let QS_ALLINPUT = 0x04FFu

    [<Literal>]
    let WAIT_OBJECT_0 = 0u

    [<Literal>]
    let WAIT_TIMEOUT = 258u

    [<Literal>]
    let PM_REMOVE = 0x0001u

    [<Literal>]
    let INFINITE = 0xFFFFFFFFu

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type MSG =
        val mutable hwnd: nativeint
        val mutable message: uint32
        val mutable wParam: nativeint
        val mutable lParam: nativeint
        val mutable time: uint32
        val mutable pt_x: int32
        val mutable pt_y: int32

    [<DllImport("ole32.dll")>]
    extern int CoInitializeEx(nativeint pvReserved, uint32 coInit)

    [<DllImport("ole32.dll")>]
    extern unit CoUninitialize()

    [<DllImport("kernel32.dll")>]
    extern uint32 GetCurrentThreadId()

    [<DllImport("user32.dll", SetLastError = true)>]
    extern bool PostThreadMessage(uint32 idThread, uint32 Msg, nativeint wParam, nativeint lParam)

    [<DllImport("user32.dll")>]
    extern int GetMessage(MSG& lpMsg, nativeint hWnd, uint32 wMsgFilterMin, uint32 wMsgFilterMax)

    [<DllImport("user32.dll")>]
    extern bool TranslateMessage(MSG& lpMsg)

    [<DllImport("user32.dll")>]
    extern nativeint DispatchMessage(MSG& lpMsg)

    [<DllImport("user32.dll")>]
    extern bool PeekMessage(MSG& lpMsg, nativeint hWnd, uint32 wMsgFilterMin, uint32 wMsgFilterMax, uint32 wRemoveMsg)

/// Work item for the STA thread dispatcher queue.
[<NoEquality; NoComparison>]
type private WorkItem =
    { Execute: unit -> unit
      Completion: TaskCompletionSource<obj> }

/// Dedicated STA thread dispatcher using a lightweight Win32 message loop.
/// Does not depend on WPF - works in Windows Services, Server Core, and containers.
type StaThreadDispatcher(?threadName: string) =
    let mutable disposed = 0
    let shutdownTcs = TaskCompletionSource<unit>()
    let readyTcs = TaskCompletionSource<uint32>() // Signals when thread is ready, returns thread ID
    let workQueue = ConcurrentQueue<WorkItem>()

    let startStaThread () =
        let mutable comInitialized = false

        let thread =
            Thread(
                ThreadStart(fun () ->
                    // Get the thread ID for posting messages
                    let threadId = StaNative.GetCurrentThreadId()

                    // Try to initialize COM explicitly. This may fail if:
                    // - S_FALSE (1): Already initialized with same mode - OK
                    // - RPC_E_CHANGED_MODE: Already initialized with different mode - try to continue anyway
                    //   since SetApartmentState(STA) should have set the correct mode
                    let hr = StaNative.CoInitializeEx(IntPtr.Zero, StaNative.COINIT_APARTMENTTHREADED)

                    // S_OK (0) or S_FALSE (1) means success
                    // RPC_E_CHANGED_MODE (0x80010106) means COM is already initialized -
                    // if SetApartmentState(STA) worked, we should be on an STA thread
                    comInitialized <- (hr = 0 || hr = 1)

                    // Only fail if we get a truly unexpected error (not RPC_E_CHANGED_MODE)
                    // RPC_E_CHANGED_MODE as signed int32 is -2147417850
                    let rpcChangedMode = -2147417850

                    if hr < 0 && hr <> rpcChangedMode then
                        readyTcs.SetException(Marshal.GetExceptionForHR(hr))
                    else
                        try
                            // Create a message queue for this thread by calling PeekMessage
                            // This ensures PostThreadMessage will work
                            let mutable msg = StaNative.MSG()
                            StaNative.PeekMessage(&msg, IntPtr.Zero, 0u, 0u, StaNative.PM_REMOVE) |> ignore

                            // Signal that we're ready
                            readyTcs.SetResult(threadId)

                            // Run the message loop
                            let mutable running = true

                            while running do
                                let result = StaNative.GetMessage(&msg, IntPtr.Zero, 0u, 0u)

                                if result = 0 || result = -1 then
                                    // WM_QUIT received or error - exit the loop
                                    running <- false
                                else
                                    match msg.message with
                                    | StaNative.WM_EXECUTE_WORK ->
                                        // Process all queued work items
                                        let mutable work = Unchecked.defaultof<WorkItem>

                                        while workQueue.TryDequeue(&work) do
                                            try
                                                work.Execute()
                                            with _ ->
                                                ()
                                    | _ ->
                                        // Process other messages (needed for COM message pumping)
                                        StaNative.TranslateMessage(&msg) |> ignore
                                        StaNative.DispatchMessage(&msg) |> ignore
                        finally
                            if comInitialized then
                                StaNative.CoUninitialize()

                            shutdownTcs.TrySetResult(()) |> ignore)
            )

        thread.IsBackground <- true
        thread.SetApartmentState(ApartmentState.STA)

        match threadName with
        | Some name when not (String.IsNullOrWhiteSpace name) -> thread.Name <- name
        | _ -> ()

        thread.Start()
        thread

    let staThread = startStaThread ()

    let staThreadId =
        try
            readyTcs.Task.GetAwaiter().GetResult()
        with ex ->
            shutdownTcs.TrySetResult(()) |> ignore
            // Include inner exception details in the message for better diagnostics
            let innerMsg =
                match ex.InnerException with
                | null -> ex.Message
                | inner -> $"{ex.Message} -> {inner.GetType().Name}: {inner.Message}"

            raise (InvalidOperationException($"Failed to initialise STA dispatcher for JV-Link: {innerMsg}", ex))

    let ensureNotDisposed () =
        if Volatile.Read(&disposed) <> 0 then
            raise (ObjectDisposedException("StaThreadDispatcher"))

    let isOnStaThread () =
        StaNative.GetCurrentThreadId() = staThreadId

    let queueWork (work: WorkItem) =
        workQueue.Enqueue(work)
        // Wake up the STA thread to process work
        if not (StaNative.PostThreadMessage(staThreadId, StaNative.WM_EXECUTE_WORK, IntPtr.Zero, IntPtr.Zero)) then
            // PostThreadMessage failed - remove the queued item and fail the task
            // to prevent the caller from hanging forever
            let errCode = Marshal.GetLastPInvokeError()
            let mutable discarded = Unchecked.defaultof<WorkItem>
            // Try to remove the item we just added (best effort - concurrent queue)
            workQueue.TryDequeue(&discarded) |> ignore

            let errMsg =
                $"PostThreadMessage failed with error code {errCode}. STA thread may have exited."

            Diagnostics.emit errMsg
            work.Completion.TrySetException(InvalidOperationException(errMsg)) |> ignore

    let execute name (call: unit -> 'T) : 'T =
        ensureNotDisposed ()

        let invoke () =
            try
                call ()
            with ex ->
                Diagnostics.emit $"STA dispatcher call '{name}' threw {ex.GetType().Name}: {ex.Message}"
                reraise ()

        if isOnStaThread () then
            invoke ()
        else
            let tcs =
                TaskCompletionSource<obj>(TaskCreationOptions.RunContinuationsAsynchronously)

            let work =
                { Execute =
                    fun () ->
                        try
                            let result = invoke ()
                            tcs.SetResult(box result)
                        with ex ->
                            tcs.SetException(ex)
                  Completion = tcs }

            queueWork work

            // Wait synchronously for the result
            try
                tcs.Task.GetAwaiter().GetResult() :?> 'T
            with :? AggregateException as ae when ae.InnerExceptions.Count = 1 ->
                ExceptionDispatchInfo.Capture(ae.InnerExceptions.[0]).Throw()
                Unchecked.defaultof<'T> // Never reached

    interface IComDispatcher with
        member _.Invoke<'T>(name: string, call: unit -> 'T) : 'T = execute name call

        member _.InvokeAsync<'T>(name: string, call: unit -> 'T) : Task<'T> =
            ensureNotDisposed ()

            if isOnStaThread () then
                try
                    Task.FromResult(call ())
                with ex ->
                    Task.FromException<'T>(ex)
            else
                let tcs =
                    TaskCompletionSource<obj>(TaskCreationOptions.RunContinuationsAsynchronously)

                let work =
                    { Execute =
                        fun () ->
                            try
                                let result = call ()
                                tcs.SetResult(box result)
                            with ex ->
                                Diagnostics.emit
                                    $"STA dispatcher async call '{name}' threw {ex.GetType().Name}: {ex.Message}"

                                tcs.SetException(ex)
                      Completion = tcs }

                queueWork work

                task {
                    let! result = tcs.Task
                    return result :?> 'T
                }

    member private this.Dispose(disposing: bool) =
        if Interlocked.Exchange(&disposed, 1) = 0 then
            if readyTcs.Task.IsCompletedSuccessfully then
                // Post WM_QUIT to exit the message loop
                if
                    not (StaNative.PostThreadMessage(staThreadId, uint32 StaNative.WM_QUIT, IntPtr.Zero, IntPtr.Zero))
                then
                    // PostThreadMessage failed - thread may have already exited, which is fine during disposal
                    let errCode = Marshal.GetLastPInvokeError()
                    Diagnostics.emit $"PostThreadMessage(WM_QUIT) failed with error code {errCode} during disposal"

            // Wait for message loop to exit (with timeout to avoid hangs during shutdown).
            shutdownTcs.Task.Wait(TimeSpan.FromSeconds 5.0) |> ignore

            if staThread.IsAlive then
                staThread.Join(TimeSpan.FromSeconds 5.0) |> ignore

    interface IDisposable with
        member this.Dispose() =
            this.Dispose(true)
            GC.SuppressFinalize(this)

    override this.Finalize() = this.Dispose(false)

#else

/// Placeholder implementation for non-Windows builds (COM interop not supported).
type StaThreadDispatcher() =
    let notSupported () =
        PlatformNotSupportedException("STA dispatcher is only available when targeting Windows.")

    interface IComDispatcher with
        member _.Invoke<'T>(name: string, call: unit -> 'T) : 'T = raise (notSupported ())
        member _.InvokeAsync<'T>(name: string, call: unit -> 'T) : Task<'T> = Task.FromException<'T>(notSupported ())

    interface IDisposable with
        member _.Dispose() = ()

#endif
