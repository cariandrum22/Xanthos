namespace Xanthos.WindowsTests

open System
open System.Globalization
open System.Runtime.InteropServices
open System.Threading
open Xunit
open Xanthos
open Xanthos.Interop

module private Native =
    [<DllImport("kernel32.dll")>]
    extern uint32 GetThreadLocale()

    [<DllImport("oleaut32.dll")>]
    extern int VariantClear(nativeint variant)

/// Reflection target only; no ProgID registration, COM server or SDK calls.
type ManagedDispatchTarget() =
    member _.JVInit(_sid: string) = 0

    member _.JVStatus() : int =
        raise (COMException("controlled dispatch", -2147024891))

    member _.m_JVLinkVersion = "0500-managed"

module WindowsManagedTests =
    let private requireWindows () =
        Assert.True(OperatingSystem.IsWindows())
        Assert.Equal(Architecture.X64, RuntimeInformation.ProcessArchitecture)
        Assert.Equal(8, IntPtr.Size)
        // This module exists only in the WINDOWS build; portable net10.0 fails.
        Assert.NotNull(typeof<Session>.Assembly.GetType("Xanthos.Interop.JvLinkLocale"))

    [<Fact>]
    let ``WindowsManaged executes the actual WINDOWS library on an x64 process`` () = requireWindows ()

    [<Fact>]
    let ``Windows COM client dispatch uses injected activation and release on its owned STA`` () =
        requireWindows ()
        let mutable created = 0
        let mutable released = 0
        let mutable worker: Thread option = None
        let target = ManagedDispatchTarget()

        let activation =
            { ComClientActivation.Resolve =
                fun id ->
                    Assert.Equal("test-only-no-progid", id)
                    typeof<ManagedDispatchTarget>
              Create =
                fun actual ->
                    Assert.Equal(typeof<ManagedDispatchTarget>, actual)
                    created <- created + 1
                    worker <- Some Thread.CurrentThread
                    Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState())
                    Assert.Equal(1041u, Native.GetThreadLocale())
                    box target
              Release =
                fun instance ->
                    Assert.Same(target, instance)
                    Assert.Equal(worker.Value.ManagedThreadId, Thread.CurrentThread.ManagedThreadId)
                    released <- released + 1 }

        let client = new ComJvLinkClient(activation, progId = "test-only-no-progid")
        let session = new Session(client :> INativeJvLink)

        try
            Assert.Equal(Ok(), JvLink.init "UNKNOWN" session)
            Assert.Equal(Ok "0500-managed", JvLink.getVersion session)

            match JvLink.status session with
            | Error error ->
                Assert.Equal("JVStatus", error.Api)
                Assert.Equal(Some -2147024891, error.Code)
                Assert.Equal("controlled dispatch", error.Message)
            | Ok _ -> failwith "Native dispatch exception lost"
        finally
            (session :> IDisposable).Dispose()

        (client :> IDisposable).Dispose()
        Assert.Equal(1, created)
        Assert.Equal(1, released)
        Assert.False(worker.Value.IsAlive)

        match JvLink.getVersion session with
        | Error error -> Assert.Equal(JvErrorKind.Disposed, error.Kind)
        | Ok _ -> failwith "Disposed dispatch accepted"

    [<Fact>]
    let ``Owned STA supports reentrancy exceptions shutdown and Japanese native locale`` () =
        requireWindows ()
        let caller = Thread.CurrentThread.ManagedThreadId
        let culture = CultureInfo.CurrentCulture
        let nativeLocale = Native.GetThreadLocale()

        let dispatcher =
            new StaThreadDispatcher("Xanthos WindowsManaged test") :> IComDispatcher

        let mutable worker: Thread option = None

        try
            let observed =
                dispatcher.Invoke(
                    "locale",
                    fun () ->
                        worker <- Some Thread.CurrentThread
                        Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState())
                        Assert.NotEqual(caller, Thread.CurrentThread.ManagedThreadId)
                        JvLinkLocale.initialize ()
                        Assert.Equal(1041u, Native.GetThreadLocale())
                        Assert.Equal("ja-JP", CultureInfo.CurrentCulture.Name)
                        dispatcher.Invoke("nested", fun () -> Thread.CurrentThread.ManagedThreadId)
                )

            Assert.Equal(worker.Value.ManagedThreadId, observed)

            let error =
                Assert.Throws<COMException>(fun () ->
                    dispatcher.Invoke("throw", fun () -> raise (COMException("controlled", -2147467259)))
                    |> ignore)

            Assert.Equal(-2147467259, error.HResult)
            Assert.Equal(17, dispatcher.Invoke("recovery", fun () -> 17))
            Assert.Equal(culture, CultureInfo.CurrentCulture)
            Assert.Equal(nativeLocale, Native.GetThreadLocale())
        finally
            dispatcher.Dispose()

        Assert.False(worker.Value.IsAlive, "Owned STA survived dispatcher disposal")

        Assert.Throws<ObjectDisposedException>(fun () -> dispatcher.Invoke("late", fun () -> 0) |> ignore)
        |> ignore

    [<Theory; InlineData(-2147483648L); InlineData(0L); InlineData(2147483647L)>]
    let ``x64 HWND retains the SDK signed I4 contract`` (value: int64) =
        requireWindows ()

        match ComInterop.windowHandleToInt32 (nativeint value) with
        | Ok actual -> Assert.Equal(int value, actual)
        | Error error -> failwithf "%A" error

    [<Theory; InlineData(-2147483649L); InlineData(2147483648L)>]
    let ``x64 HWND rejects values outside signed I4 without truncation`` (value: int64) =
        requireWindows ()

        match ComInterop.windowHandleToInt32 (nativeint value) with
        | Error(Core.ComError.InvalidInput _) -> ()
        | other -> failwithf "Expected out-of-range error: %A" other

    [<Fact>]
    let ``Native BSTR retains embedded null and CP932 byte boundaries`` () =
        requireWindows ()
        let text = "中央競馬\000A馬"
        let bstr = Marshal.StringToBSTR text

        try
            let restored = Marshal.PtrToStringBSTR bstr
            Assert.Equal(text, restored)
            Assert.Equal<byte>(Core.Text.encodeShiftJis text, ComInterop.readBstrBytes restored)
        finally
            Marshal.FreeBSTR bstr

        Assert.Throws<Text.EncoderFallbackException>(fun () -> ComInterop.readBstrBytes "😀" |> ignore)
        |> ignore

    [<Fact>]
    let ``Native VARIANT SAFEARRAY roundtrip retains full unsigned byte values`` () =
        requireWindows ()
        let bytes = [| for value in 0..255 -> byte value |]
        // VARIANT is 24 bytes on x64; VariantClear owns its SAFEARRAY allocation.
        let variant = Marshal.AllocCoTaskMem 24

        for offset in 0..23 do
            Marshal.WriteByte(variant, offset, 0uy)

        try
            Marshal.GetNativeVariantForObject(bytes, variant)
            let actual = Marshal.GetObjectForNativeVariant variant :?> byte[]
            Assert.Equal<byte>(bytes, actual)
        finally
            let hr = Native.VariantClear variant
            Marshal.FreeCoTaskMem variant
            Assert.Equal(0, hr)

    [<Fact>]
    let ``Windows registration rollback preserves original HRESULT and attempts all releases`` () =
        requireWindows ()
        let removed = ResizeArray<int>()

        let registrations =
            [ 1, Action<string>(ignore) :> Delegate
              2, Action<string>(ignore) :> Delegate
              3, Action<string>(ignore) :> Delegate ]

        let attach id _ =
            if id = 3 then
                raise (COMException("controlled-registration", -2147467259))

        let detach id _ = removed.Add id

        match EventRegistration.attach attach detach registrations with
        | Error error -> Assert.Equal(Some -2147467259, error.Code)
        | Ok _ -> failwith "Registration failure accepted"

        Assert.Equal<int>([| 2; 1 |], removed)

    [<Fact>]
    let ``Windows deregistration attempts every handler and preserves first HRESULT and failed IDs`` () =
        requireWindows ()
        let attempted = ResizeArray<int>()
        let registrations = [ for id in 1..4 -> id, Action<string>(ignore) :> Delegate ]

        let detach id _ =
            attempted.Add id

            if id = 1 then
                raise (COMException("first removal", -2147467259))

            if id = 3 then
                raise (COMException("later removal", -2147024891))

        match EventRegistration.detach detach registrations with
        | Error error ->
            Assert.Equal("ComEventsHelper.Remove", error.Api)
            Assert.Equal(JvErrorKind.Invocation, error.Kind)
            Assert.Equal(Some -2147467259, error.Code)
            Assert.Equal("first removal", error.Message)
            Assert.Equal("1,3", error.Outputs["failedDispids"])
        | Ok _ -> failwith "Deregistration failure accepted"

        Assert.Equal<int>([| 1; 2; 3; 4 |], attempted)

    [<Fact>]
    let ``Windows rollback retains registration HRESULT when removal also fails`` () =
        requireWindows ()
        let attempted = ResizeArray<int>()
        let registrations = [ for id in 1..3 -> id, Action<string>(ignore) :> Delegate ]

        let attach id _ =
            if id = 3 then
                raise (COMException("registration", -2147467259))

        let detach id _ =
            attempted.Add id

            if id = 2 then
                raise (COMException("removal", -2147024891))

        match EventRegistration.attach attach detach registrations with
        | Error error ->
            Assert.Equal("ComEventsHelper.Combine", error.Api)
            Assert.Equal(Some -2147467259, error.Code)
            Assert.Equal("registration", error.Message)
            Assert.Equal("2", error.Outputs["failedDispids"])
            Assert.Equal("removal", error.Outputs["cleanupError"])
        | Ok _ -> failwith "Rollback failure accepted"

        Assert.Equal<int>([| 2; 1 |], attempted)
