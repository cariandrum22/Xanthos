namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos
open Xanthos.Functional

/// Independent controlled native boundary, not the legacy JvLinkStub.
type internal NativeFixture() =
    let calls = ResizeArray<string>()
    let mutable disposed = false
    let mutable callback: (JvEvent -> unit) option = None
    let mutable savePath = "initial"
    member val Handler: (string -> obj[] -> Result<obj, JvError> option) = (fun _ _ -> None) with get, set
    member _.Calls = calls |> Seq.toList
    member _.Disposed = disposed
    member val CleanupError: JvError option = None with get, set
    member val CurrentFileTimestamp: obj = box "unparsed-value" with get, set

    member _.Emit kind key =
        callback |> Option.iter (fun action -> action { Kind = kind; RawKey = key })

    interface INativeJvLink with
        member this.Invoke(api, arguments, _) =
            calls.Add api

            match this.Handler api arguments with
            | Some value -> value
            | None ->
                match api with
                | "JVOpen" ->
                    arguments.[3] <- box 23
                    arguments.[4] <- box 7
                    arguments.[5] <- box "20260911235959"
                    Ok(box 0)
                | "JVSetSavePath" ->
                    savePath <- unbox arguments.[0]
                    Ok(box 0)
                | "JVRead"
                | "JVGets" ->
                    let raw = Core.Text.encodeShiftJis "RA日本"
                    Text.Encoding.RegisterProvider(Text.CodePagesEncodingProvider.Instance)

                    arguments.[0] <-
                        if api = "JVRead" then
                            box (Text.Encoding.GetEncoding(932).GetString raw)
                        else
                            box raw

                    arguments.[2] <- box "source.jvd"
                    Ok(box 6)
                | "JVFuku" ->
                    arguments.[1] <- box [| 66uy; 77uy |]
                    Ok(box -1)
                | "JVCourseFile" ->
                    arguments.[1] <- box "course.bmp"
                    arguments.[2] <- box "説明\r\n"
                    Ok(box 0)
                | "JVMVRead" ->
                    arguments.[0] <- box "entry"
                    arguments.[1] <- box 5
                    Ok(box 5)
                | "JVSkip"
                | "JVCancel" -> Ok null
                | _ -> Ok(box 0)

        member this.Get name =
            calls.Add name

            match name with
            | "m_savepath" -> Ok(box savePath)
            | "m_servicekey" -> Ok(box "synthetic-fixture")
            | "m_JVLinkVersion" -> Ok(box "0500")
            | "m_CurrentFileTimestamp" -> Ok this.CurrentFileTimestamp
            | _ -> Ok(box 1)

        member _.Put(name, _) =
            calls.Add name
            Ok()

        member _.Watch action =
            calls.Add "JVWatchEvent"
            callback <- Some action
            Ok()

        member _.StopWatch() =
            calls.Add "JVWatchEventClose"
            callback <- None
            Ok()

        member this.Dispose() =
            disposed <- true
            callback <- None

            this.CleanupError
            |> Option.iter (fun error -> raise (SessionCleanupException error))

module FunctionalApiTests =
    [<Fact; Trait("Category", "Contract")>]
    let ``Disconnect preserves the original SDK cleanup error`` () =
        let native = new NativeFixture()

        let expected =
            { Api = "JVClose"
              Code = Some -999
              Kind = JvErrorKind.Sdk
              Outputs = Map.empty
              Message = "Injected close failure" }

        native.CleanupError <- Some expected
        use session = new Session(native)
        Assert.Equal(Error expected, JvLink.disconnect session)
        Assert.True(native.Disposed)

    let private success =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("JVInit"); InlineData("JVSetUIProperties"); InlineData("JVSetServiceKey")>]
    [<InlineData("JVSetSaveFlag"); InlineData("JVSetSavePath"); InlineData("JVOpen"); InlineData("JVRTOpen")>]
    [<InlineData("JVStatus"); InlineData("JVRead"); InlineData("JVGets"); InlineData("JVSkip")>]
    [<InlineData("JVCancel"); InlineData("JVClose"); InlineData("JVFiledelete"); InlineData("JVFukuFile")>]
    [<InlineData("JVFuku"); InlineData("JVMVCheck"); InlineData("JVMVCheckWithType"); InlineData("JVMVPlay")>]
    [<InlineData("JVMVPlayWithType"); InlineData("JVMVOpen"); InlineData("JVMVRead"); InlineData("JVCourseFile")>]
    [<InlineData("JVCourseFile2"); InlineData("JVWatchEvent"); InlineData("JVWatchEventClose")>]
    [<InlineData("m_saveflag"); InlineData("m_savepath"); InlineData("m_servicekey"); InlineData("m_JVLinkVersion")>]
    [<InlineData("m_TotalReadFilesize"); InlineData("m_CurrentReadFilesize"); InlineData("m_CurrentFileTimestamp")>]
    [<InlineData("ParentHWnd"); InlineData("m_payflag")>]
    let ``External FSharp project invokes every official operation through functions`` api =
        let native = new NativeFixture()
        use session = new Session(native)
        let examples = Examples.apiExamples "synthetic-key" "owned-output" session
        Assert.Equal(35, examples.Length)
        let invoke = examples |> List.find (fun (name, _) -> name = api) |> snd
        invoke () |> success
        Assert.Equal<string list>([ api ], native.Calls)

    [<Fact; Trait("Category", "Contract")>]
    let ``Partial application and pipeline preserve open out values`` () =
        use session = new Session(new NativeFixture())

        let expected =
            OpenOutcome.Opened
                { ReadCount = 23
                  DownloadCount = 7
                  LastFileTimestamp = "20260911235959" }

        Assert.Equal(Ok expected, Examples.openViaPipeline session)
        Assert.Equal(Examples.openViaPipeline session, JvLink.openData Examples.request session)

    [<Fact; Trait("Category", "Contract")>]
    let ``Error in composed workflow disposes its explicit owner`` () =
        let native = new NativeFixture()
        native.Handler <- fun api _ -> if api = "JVOpen" then Some(Ok(box -305)) else None

        let result =
            use session = new Session(native)
            Examples.fetchFirst session

        match result with
        | Error error ->
            Assert.Equal("JVOpen", error.Api)
            Assert.Equal(Some -305, error.Code)
        | Ok _ -> failwith "Expected SDK refusal"

        Assert.True(native.Disposed)
        Assert.DoesNotContain("JVGets", native.Calls)

    [<Fact; Trait("Category", "Contract")>]
    let ``External workflow preserves explicit close failure`` () =
        let native = new NativeFixture()
        native.Handler <- fun api _ -> if api = "JVClose" then Some(Ok(box -201)) else None

        let result =
            use session = new Session(native)
            Examples.fetchFirst session

        match result with
        | Error error ->
            Assert.Equal("JVClose", error.Api)
            Assert.Equal(Some -201, error.Code)
        | Ok _ -> failwith "Close failure became success"

        Assert.True(native.Disposed)

    [<Fact; Trait("Category", "Contract")>]
    let ``Two sessions isolate settings events and disconnect`` () =
        let firstNative = new NativeFixture()
        let secondNative = new NativeFixture()
        use first = new Session(firstNative)
        use second = new Session(secondNative)
        JvLink.setSavePath "first" first |> success
        JvLink.setSavePath "second" second |> success
        Assert.Equal(Ok "first", JvLink.getSavePath first)
        Assert.Equal(Ok "second", JvLink.getSavePath second)
        let firstEvents = Collections.Concurrent.ConcurrentQueue<JvEvent>()
        let secondEvents = Collections.Concurrent.ConcurrentQueue<JvEvent>()
        JvLink.watchEvent firstEvents.Enqueue first |> success
        JvLink.watchEvent secondEvents.Enqueue second |> success
        firstNative.Emit EventKind.Pay "202609120511"
        secondNative.Emit EventKind.Weight "202609120511"
        Assert.True(Threading.SpinWait.SpinUntil((fun () -> firstEvents.Count = 1 && secondEvents.Count = 1), 5000))
        Assert.Equal(EventKind.Pay, firstEvents.ToArray().[0].Kind)
        Assert.Equal(EventKind.Weight, secondEvents.ToArray().[0].Kind)
        JvLink.disconnect first |> success
        JvLink.disconnect first |> success
        firstNative.Emit EventKind.Pay "ignored"
        secondNative.Emit EventKind.Weight "still-active"
        Assert.Single(firstEvents) |> ignore
        Assert.True(Threading.SpinWait.SpinUntil((fun () -> secondEvents.Count = 2), 5000))
        Assert.False(secondNative.Disposed)

        match JvLink.getVersion first with
        | Error error -> Assert.Equal(JvErrorKind.Disposed, error.Kind)
        | Ok _ -> failwith "Disposed session was usable"

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData(-305); InlineData(-123456)>]
    let ``SDK errors preserve original code and never retry`` code =
        let native = new NativeFixture()
        native.Handler <- fun _ _ -> Some(Ok(box code))
        use session = new Session(native)

        match JvLink.init "UNKNOWN" session with
        | Error error ->
            Assert.Equal(Some code, error.Code)
            Assert.Equal("JVInit", error.Api)
        | Ok _ -> failwith "SDK error became success"

        Assert.Single(native.Calls) |> ignore

    [<Fact; Trait("Category", "Contract")>]
    let ``Raw read image and property values survive the function boundary`` () =
        use session = new Session(new NativeFixture())
        let read = JvLink.gets session |> success
        Assert.Equal("RA日本", JvLink.decodeShiftJis read.Data)
        Assert.Equal("source.jvd", read.Filename)
        Assert.Equal(6, read.ByteCount)
        let image = JvLink.silksBinary "pattern" session |> success
        Assert.Equal(ImageState.NoImage, image.State)
        Assert.Equal<byte>([| 66uy; 77uy |], image.Value)
        Assert.Equal(Ok "unparsed-value", JvLink.getCurrentFileTimestamp session)
        Assert.Equal(Ok { RawKilobytes = 1; Bytes = 1024L }, JvLink.getTotalReadFileSize session)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData(null, ""); InlineData("", ""); InlineData("20260912010203", "20260912010203")>]
    let ``Current timestamp accepts unset BSTR and preserves SDK text`` (value: string) expected =
        let native = new NativeFixture()
        native.CurrentFileTimestamp <- box value
        use session = new Session(native)
        Assert.Equal(Ok expected, JvLink.getCurrentFileTimestamp session)

    [<Fact; Trait("Category", "Contract")>]
    let ``Current timestamp rejects non string SDK values`` () =
        let native = new NativeFixture()
        native.CurrentFileTimestamp <- box 42
        use session = new Session(native)

        match JvLink.getCurrentFileTimestamp session with
        | Error error ->
            Assert.Equal("m_CurrentFileTimestamp", error.Api)
            Assert.Equal(JvErrorKind.Invocation, error.Kind)
        | Ok _ -> failwith "Invalid property type became a timestamp"

    [<Fact; Trait("Category", "Contract")>]
    let ``All seven event origins remain distinct with the same raw key`` () =
        let native = new NativeFixture()
        use session = new Session(native)
        let events = Collections.Concurrent.ConcurrentQueue<JvEvent>()
        use subscription = JvLink.subscribe events.Enqueue session |> success

        let kinds =
            [ EventKind.Pay
              EventKind.JockeyChange
              EventKind.Weather
              EventKind.CourseChange
              EventKind.Avoid
              EventKind.TimeChange
              EventKind.Weight ]

        for kind in kinds do
            native.Emit kind "same-raw-key"

        Assert.True(Threading.SpinWait.SpinUntil((fun () -> events.Count = 7), 5000))
        Assert.Equal<EventKind list>(kinds, events |> Seq.map _.Kind |> Seq.toList)
        Assert.Equal(7, events |> Seq.map Examples.eventOrigin |> Seq.distinct |> Seq.length)
        subscription.Dispose()
        native.Emit EventKind.Pay "ignored"
        Assert.Equal(7, events.Count)

    [<Fact; Trait("Category", "Contract")>]
    let ``JVRead restores Japanese BSTR text to exact CP932 record bytes`` () =
        let native = new NativeFixture()

        native.Handler <-
            fun api args ->
                if api = "JVRead" then
                    args.[0] <- box "ク日本"
                    args.[2] <- box "byte-probe.jvd"
                    Some(Ok(box 6))
                else
                    None

        use session = new Session(native)
        let result = JvLink.read session |> success
        Assert.Equal<byte>([| 0x83uy; 0x4euy; 0x93uy; 0xfauy; 0x96uy; 0x7buy |], result.Data)
        Assert.Equal(Some "ク日本", result.RawText)

    [<Fact; Trait("Category", "Contract")>]
    let ``Unrepresentable native text yields a typed invocation error`` () =
        let native = new NativeFixture()

        native.Handler <-
            fun api args ->
                if api = "JVRead" then
                    args.[0] <- box "\U0001F40E"
                    Some(Ok(box 4))
                else
                    None

        use session = new Session(native)

        match JvLink.read session with
        | Error error ->
            Assert.Equal(JvErrorKind.Invocation, error.Kind)
            Assert.Equal("JVRead", error.Api)
        | Ok _ -> failwith "Unsupported native representation was silently changed"
