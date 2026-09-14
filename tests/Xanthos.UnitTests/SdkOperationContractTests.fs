namespace Xanthos.UnitTests

open System
open System.IO
open System.Text.Json
open Xunit
open Xanthos
open Xanthos.Functional

/// The return-code table is transcribed from PDF pp.52-65, independently of production mappings.
module private SdkContractOracle =
    let methodNames =
        [ "JVInit"
          "JVSetUIProperties"
          "JVSetServiceKey"
          "JVSetSaveFlag"
          "JVSetSavePath"
          "JVOpen"
          "JVRTOpen"
          "JVStatus"
          "JVRead"
          "JVGets"
          "JVSkip"
          "JVCancel"
          "JVClose"
          "JVFiledelete"
          "JVFukuFile"
          "JVFuku"
          "JVMVCheck"
          "JVMVCheckWithType"
          "JVMVPlay"
          "JVMVPlayWithType"
          "JVMVOpen"
          "JVMVRead"
          "JVCourseFile"
          "JVCourseFile2"
          "JVWatchEvent"
          "JVWatchEventClose" ]

    let propertyNames =
        [ "m_saveflag"
          "m_savepath"
          "m_servicekey"
          "m_JVLinkVersion"
          "m_TotalReadFilesize"
          "m_CurrentReadFilesize"
          "m_CurrentFileTimestamp"
          "ParentHWnd"
          "m_payflag" ]

    let invoke api session =
        match api with
        | "JVOpen" -> JvLink.openData Examples.request session |> Result.map box
        | "JVRTOpen" -> JvLink.openRealtime "0B12" "202609120511" session |> Result.map box
        | "JVStatus" -> JvLink.status session |> Result.map box
        | "JVRead" -> JvLink.read session |> Result.map box
        | "JVGets" -> JvLink.gets session |> Result.map box
        | "JVFuku" -> JvLink.silksBinary "synthetic-pattern" session |> Result.map box
        | "JVFukuFile" -> JvLink.silksFile "synthetic-pattern" "owned.bmp" session |> Result.map box
        | "JVCourseFile" -> JvLink.courseFile "9999999905240011" session |> Result.map box
        | "JVCourseFile2" -> JvLink.courseFile2 "9999999905240011" "owned.gif" session |> Result.map box
        | "JVMVCheck" -> JvLink.movieCheck "202609120511" session |> Result.map box
        | "JVMVCheckWithType" -> JvLink.movieCheckWithType "00" "202609120511" session |> Result.map box
        | "JVMVOpen" -> JvLink.movieOpen "11" "20260912" session |> Result.map box
        | "JVMVRead" -> JvLink.movieReadWithCapacity 21 session |> Result.map box
        | _ ->
            Examples.apiExamples "synthetic-fixture" "owned-output" session
            |> List.find (fun (name, _) -> name = api)
            |> snd
            |> fun action -> action () |> Result.map box

    let error api kind code =
        { Api = api
          Code = Some code
          Kind = kind
          Outputs = Map.empty
          Message = "Independent contract fixture" }

    let value =
        function
        | Ok result -> result
        | Error error -> failwithf "%A" error

type private SdkContractNative() =
    let calls = ResizeArray<string * obj[] * int list>()
    member _.Calls = List.ofSeq calls
    member val Code = 0 with get, set
    member val Fault: JvError option = None with get, set
    member val OnInvoke: (string -> obj[] -> unit) = (fun _ _ -> ()) with get, set
    member val PropertyValue: obj = box 17 with get, set

    interface INativeJvLink with
        member this.Invoke(api, args, indices) =
            calls.Add(api, Array.copy args, indices)
            this.OnInvoke api args

            match this.Fault with
            | Some error -> Error error
            | None ->
                match api with
                | "JVSkip"
                | "JVCancel" -> Ok null
                | _ -> Ok(box this.Code)

        member this.Get api =
            calls.Add(api, [||], [])

            match this.Fault with
            | Some error -> Error error
            | None -> Ok this.PropertyValue

        member this.Put(api, value) =
            calls.Add(api, [| value |], [])

            match this.Fault with
            | Some error -> Error error
            | None -> Ok()

        member this.Watch _ =
            calls.Add("JVWatchEvent", [||], [])

            match this.Fault with
            | Some error -> Error error
            | None when this.Code = 0 -> Ok()
            | None -> Error(SdkContractOracle.error "JVWatchEvent" JvErrorKind.Sdk this.Code)

        member this.StopWatch() =
            calls.Add("JVWatchEventClose", [||], [])

            match this.Fault with
            | Some error -> Error error
            | None when this.Code = 0 -> Ok()
            | None -> Error(SdkContractOracle.error "JVWatchEventClose" JvErrorKind.Sdk this.Code)

        member _.Dispose() = ()

type SdkOperationContractTests() =
    [<Theory; InlineData("JVRead"); InlineData("JVGets"); Trait("Category", "Contract")>]
    member _.``Corrupt record failure preserves the filename needed for recovery``(api: string) =
        let native = new SdkContractNative(Code = -403)
        native.OnInvoke <- fun _ args -> args.[2] <- box "damaged-file.jvd"
        use session = new Session(native)

        match SdkContractOracle.invoke api session with
        | Error error ->
            Assert.Equal(api, error.Api)
            Assert.Equal(Some -403, error.Code)
            Assert.Equal(Some "damaged-file.jvd", Map.tryFind "filename" error.Outputs)
            Assert.Equal(Some "131072", Map.tryFind "size" error.Outputs)
        | Ok _ -> failwith "Corrupt record became success"

    [<Theory;
      InlineData("JVRead", 0);
      InlineData("JVRead", -1);
      InlineData("JVRead", -3);
      InlineData("JVGets", 0);
      InlineData("JVGets", -1);
      InlineData("JVGets", -3);
      InlineData("JVMVRead", 0);
      InlineData("JVMVRead", -3);
      Trait("Category", "Contract")>]
    member _.``No record outcomes accept absent native output buffers``(api: string, code: int) =
        let native = new SdkContractNative(Code = code)

        native.OnInvoke <-
            fun _ args ->
                args.[0] <- null

                if args.Length = 3 then
                    args.[2] <- null

        use session = new Session(native)
        SdkContractOracle.invoke api session |> SdkContractOracle.value |> ignore

    static member ReturnCodes =
        use doc =
            JsonDocument.Parse(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts/sdk-return-codes.json"))
            )

        doc.RootElement.GetProperty("cases").EnumerateArray()
        |> Seq.map (fun row ->
            [| box (row.GetProperty("api").GetString())
               box (row.GetProperty("code").GetInt32())
               box (row.GetProperty("page").GetInt32()) |])
        |> Seq.toArray

    static member Methods = SdkContractOracle.methodNames |> Seq.map (fun api -> [| box api |])

    static member Operations =
        SdkContractOracle.methodNames @ SdkContractOracle.propertyNames
        |> Seq.map (fun api -> [| box api |])

    [<Theory; MemberData(nameof SdkOperationContractTests.ReturnCodes); Trait("Category", "Contract")>]
    member _.``Every documented SDK code retains its operation and outcome``(api: string, code: int, page: int) =
        Assert.InRange(page, 52, 65)
        let native = new SdkContractNative(Code = code)
        use session = new Session(native)
        let result = SdkContractOracle.invoke api session

        let special =
            match api with
            | "JVOpen"
            | "JVRTOpen"
            | "JVMVOpen"
            | "JVFuku"
            | "JVFukuFile"
            | "JVCourseFile"
            | "JVCourseFile2"
            | "JVMVCheck"
            | "JVMVCheckWithType" -> code = -1
            | "JVRead"
            | "JVGets" -> code = -1 || code = -3
            | "JVMVRead" -> code = -3
            | _ -> false

        if code < 0 && not special then
            match result with
            | Error error ->
                Assert.Equal(api, error.Api)
                Assert.Equal(Some code, error.Code)
                Assert.Equal(JvErrorKind.Sdk, error.Kind)
            | Ok _ -> failwithf "PDF p.%d: %s/%d became success" page api code
        else
            let output = SdkContractOracle.value result

            match api with
            | "JVOpen" ->
                match unbox<OpenOutcome> output, code with
                | OpenOutcome.Opened _, 0
                | OpenOutcome.NoData _, -1 -> ()
                | actual -> failwithf "Wrong open outcome: %A" actual
            | "JVRTOpen" ->
                Assert.Equal(
                    (if code = 0 then
                         RealtimeOpenOutcome.Opened
                     else
                         RealtimeOpenOutcome.NoData),
                    unbox output
                )
            | "JVMVOpen" ->
                Assert.Equal(
                    (if code = 0 then
                         VideoOpenOutcome.Opened
                     else
                         VideoOpenOutcome.NoData),
                    unbox output
                )
            | "JVRead"
            | "JVGets" ->
                let read = unbox<ReadResult> output

                let expected =
                    match code with
                    | -1 -> ReadState.FileBoundary
                    | -3 -> ReadState.DownloadPending
                    | _ -> ReadState.EndOfStream

                Assert.Equal(expected, read.State)
                Assert.Equal(code, read.ReturnCode)
            | "JVMVRead" ->
                match unbox<VideoReadOutcome> output, code with
                | VideoReadOutcome.EndOfStream _, 0
                | VideoReadOutcome.DownloadPending _, -3 -> ()
                | actual -> failwithf "Wrong movie outcome: %A" actual
            | "JVMVCheck"
            | "JVMVCheckWithType" ->
                let expected =
                    match code with
                    | 1 -> VideoAvailability.Available
                    | 0 -> VideoAvailability.Unpublished
                    | _ -> VideoAvailability.Missing

                Assert.Equal(expected, unbox output)
            | "JVFuku" -> Assert.Equal(code, (unbox<ImageResult<byte[]>> output).ReturnCode)
            | "JVFukuFile"
            | "JVCourseFile2" -> Assert.Equal(code, (unbox<ImageResult<string>> output).ReturnCode)
            | "JVCourseFile" -> Assert.Equal(code, (unbox<ImageResult<CourseImage>> output).ReturnCode)
            | _ -> ()

        Assert.Single(native.Calls) |> ignore

    [<Theory; MemberData(nameof SdkOperationContractTests.Methods); Trait("Category", "Contract")>]
    member _.``Unknown negative codes remain errors except void operations``(api: string) =
        let native = new SdkContractNative(Code = -87654)
        use session = new Session(native)

        match SdkContractOracle.invoke api session with
        | Ok _ when api = "JVSkip" || api = "JVCancel" -> ()
        | Error error -> Assert.Equal(Some -87654, error.Code)
        | Ok _ -> failwith "Unknown negative SDK code became success"

    [<Theory; MemberData(nameof SdkOperationContractTests.Operations); Trait("Category", "Contract")>]
    member _.``All operations preserve invocation errors including void and properties``(api: string) =
        let expected = SdkContractOracle.error api JvErrorKind.Invocation -2147467259
        let native = new SdkContractNative(Fault = Some expected)
        use session = new Session(native)
        Assert.Equal(Error expected, SdkContractOracle.invoke api session)

    [<Theory; InlineData(0); InlineData(-1); Trait("Category", "Contract")>]
    member _.``Open range and all out parameters survive success and no data``(code: int) =
        let native = new SdkContractNative(Code = code)

        native.OnInvoke <-
            fun api args ->
                Assert.Equal("JVOpen", api)
                Assert.Equal("RACE", unbox<string> args.[0])
                Assert.Equal("20260905000000-20260911235959", unbox<string> args.[1])
                Assert.Equal(1, unbox<int> args.[2])
                args.[3] <- box 37
                args.[4] <- box 19
                args.[5] <- box "unparsed-native-timestamp"

        use session = new Session(native)
        let outcome = JvLink.openData Examples.request session |> SdkContractOracle.value

        let metadata =
            match outcome with
            | OpenOutcome.Opened value
            | OpenOutcome.NoData value -> value

        Assert.Equal(
            { ReadCount = 37
              DownloadCount = 19
              LastFileTimestamp = "unparsed-native-timestamp" },
            metadata
        )

        let _, _, indices = Assert.Single(native.Calls)
        Assert.Equal<int list>([ 3; 4; 5 ], indices)

    [<Fact; Trait("Category", "Contract")>]
    member _.``Reversed range is rejected before COM``() =
        let native = new SdkContractNative()
        use session = new Session(native)

        match
            JvLink.openData
                { Examples.request with
                    ToTime = Some(DateTime(2020, 1, 1)) }
                session
        with
        | Error error -> Assert.Equal(JvErrorKind.InvalidInput, error.Kind)
        | Ok _ -> failwith "Reversed range accepted"

        Assert.Empty(native.Calls)

    [<Theory;
      InlineData("TOKU");
      InlineData("DIFF");
      InlineData("DIFN");
      InlineData("HOSE");
      InlineData("HOSN");
      InlineData("HOYU");
      Trait("Category", "Contract")>]
    member _.``SDK retains authority over dataspec range restrictions returning no data``(dataspec: string) =
        let native = new SdkContractNative(Code = -1)
        use session = new Session(native)

        match
            JvLink.openData
                { Examples.request with
                    Dataspec = dataspec }
                session
            |> SdkContractOracle.value
        with
        | OpenOutcome.NoData _ -> ()
        | _ -> failwith "PDF p.18 no-data restriction was lost"

        let _, args, _ = Assert.Single(native.Calls)
        Assert.Equal(dataspec, unbox<string> args.[0])
        Assert.Equal("20260905000000-20260911235959", unbox<string> args.[1])

    [<Theory;
      InlineData("20260912");
      InlineData("202609120511");
      InlineData("2026091205040211");
      InlineData("JC20260912051120260912121000");
      Trait("Category", "Contract")>]
    member _.``Realtime accepts every official key shape without alteration``(key: string) =
        let native = new SdkContractNative()
        use session = new Session(native)
        JvLink.openRealtime "0B16" key session |> SdkContractOracle.value |> ignore
        let api, args, indices = Assert.Single(native.Calls)
        Assert.Equal("JVRTOpen", api)
        Assert.Equal<obj[]>([| box "0B16"; box key |], args)
        Assert.Empty(indices)

    [<Theory;
      InlineData("11", "20260912");
      InlineData("12", "202609122020100001");
      InlineData("13", "2020100001");
      Trait("Category", "Contract")>]
    member _.``Movie list requests preserve all three key formats``(movieType: string, key: string) =
        let native = new SdkContractNative()
        use session = new Session(native)
        Assert.Equal(Ok VideoOpenOutcome.Opened, JvLink.movieOpen movieType key session)
        let api, args, indices = Assert.Single(native.Calls)
        Assert.Equal("JVMVOpen", api)
        Assert.Equal<obj[]>([| box movieType; box key |], args)
        Assert.Empty(indices)

    [<Theory; InlineData("JVRead"); InlineData("JVGets"); Trait("Category", "Contract")>]
    member _.``Reads preserve bytes filename length and native byref arguments``(api: string) =
        let bytes = [| 0x52uy; 0x41uy; 0x93uy; 0xfauy; 0x96uy; 0x7buy |]
        let native = new SdkContractNative(Code = bytes.Length)

        native.OnInvoke <-
            fun _ args ->
                Assert.Equal(131072, unbox<int> args.[1])

                args.[0] <-
                    if api = "JVGets" then
                        box bytes
                    else
                        box (Text.Encoding.GetEncoding(932).GetString bytes)

                args.[2] <- box "独立.jvd"

        Text.Encoding.RegisterProvider(Text.CodePagesEncodingProvider.Instance)
        use session = new Session(native)

        let result =
            SdkContractOracle.invoke api session
            |> SdkContractOracle.value
            |> unbox<ReadResult>

        Assert.Equal(ReadState.Record, result.State)
        Assert.Equal<byte>(bytes, result.Data)
        Assert.Equal("独立.jvd", result.Filename)
        Assert.Equal(6, result.ByteCount)
        Assert.Equal("RA日本", JvLink.decodeShiftJis result.Data)
        let _, _, indices = Assert.Single(native.Calls)
        Assert.Equal<int list>((if api = "JVRead" then [ 0; 1; 2 ] else [ 0; 2 ]), indices)

    [<Fact; Trait("Category", "Contract")>]
    member _.``Movie read allocates the caller buffer and preserves out size and text``() =
        let native = new SdkContractNative(Code = 20)

        native.OnInvoke <-
            fun api args ->
                Assert.Equal("JVMVRead", api)
                Assert.Equal(21, (unbox<string> args.[0]).Length)
                Assert.Equal(21, unbox<int> args.[1])
                args.[0] <- box "202609122020100001\r\n"
                args.[1] <- box 21

        use session = new Session(native)

        Assert.Equal(
            Ok(VideoReadOutcome.Record("202609122020100001\r\n", 20, 21)),
            JvLink.movieReadWithCapacity 21 session
        )

        let _, _, indices = Assert.Single(native.Calls)
        Assert.Equal<int list>([ 0; 1 ], indices)

    [<Theory; InlineData(0); InlineData(-1); Trait("Category", "Contract")>]
    member _.``Course image keeps its full path and unformatted explanation``(code: int) =
        let native = new SdkContractNative(Code = code)

        native.OnInvoke <-
            fun _ args ->
                args.[1] <- box "owned.gif"
                args.[2] <- box "説明\r\n　末尾 "

        use session = new Session(native)
        let result = JvLink.courseFile "9999999905240011" session |> SdkContractOracle.value

        Assert.Equal(
            { Filepath = "owned.gif"
              Explanation = "説明\r\n　末尾 " },
            result.Value
        )

        Assert.Equal(
            (if code = 0 then
                 ImageState.Available
             else
                 ImageState.NoImage),
            result.State
        )

    [<Fact; Trait("Category", "Contract")>]
    member _.``Positive status and kilobytes are not boolean values and do not overflow``() =
        let native = new SdkContractNative(Code = 37, PropertyValue = box Int32.MaxValue)
        use session = new Session(native)
        Assert.Equal(Ok 37, JvLink.status session)

        Assert.Equal(
            Ok
                { RawKilobytes = Int32.MaxValue
                  Bytes = 2199023254528L },
            JvLink.getTotalReadFileSize session
        )

    [<Theory; InlineData(false, 0); InlineData(true, 1); Trait("Category", "Contract")>]
    member _.``Save flag converts bool to the SDK Long without changing other arguments``
        (enabled: bool, expected: int)
        =
        let native = new SdkContractNative()
        use session = new Session(native)
        Assert.Equal(Ok(), JvLink.setSaveFlag enabled session)
        let api, args, indices = Assert.Single(native.Calls)
        Assert.Equal("JVSetSaveFlag", api)
        Assert.Equal<obj[]>([| box expected |], args)
        Assert.Empty(indices)

    [<Theory;
      InlineData("00");
      InlineData("01");
      InlineData("02");
      InlineData("03");
      InlineData("11");
      InlineData("12");
      InlineData("13");
      Trait("Category", "Contract")>]
    member _.``Movie playback accepts every documented movie type``(movieType: string) =
        let key =
            if movieType.StartsWith("1") then
                "202609122020100001"
            else
                "202609120511"

        let native = new SdkContractNative()
        use session = new Session(native)
        Assert.Equal(Ok(), JvLink.moviePlayWithType movieType key session)
        let api, args, indices = Assert.Single(native.Calls)
        Assert.Equal("JVMVPlayWithType", api)
        Assert.Equal<obj[]>([| box movieType; box key |], args)
        Assert.Empty(indices)

    [<Fact; Trait("Category", "Contract")>]
    member _.``All nine properties retain native units strings and legal access direction``() =
        let native = new SdkContractNative(PropertyValue = box "raw-native-text")
        use session = new Session(native)

        for get in
            [ JvLink.getSavePath
              JvLink.getServiceKey
              JvLink.getVersion
              JvLink.getCurrentFileTimestamp ] do
            Assert.Equal(Ok "raw-native-text", get session)

        native.PropertyValue <- box 17

        for get in [ JvLink.getSaveFlag; JvLink.getCurrentReadFileSize; JvLink.getPayFlag ] do
            Assert.Equal(Ok 17, get session)

        Assert.Equal(Ok { RawKilobytes = 17; Bytes = 17408L }, JvLink.getTotalReadFileSize session)
        Assert.Equal(Ok(), JvLink.setParentWindowHandle -123n session)

        Assert.Equal<string list>(
            [ "m_savepath"
              "m_servicekey"
              "m_JVLinkVersion"
              "m_CurrentFileTimestamp"
              "m_saveflag"
              "m_CurrentReadFilesize"
              "m_payflag"
              "m_TotalReadFilesize"
              "ParentHWnd" ],
            native.Calls |> List.map (fun (api, _, _) -> api)
        )

        let _, args, _ = List.last native.Calls
        Assert.Equal<obj[]>([| box -123 |], args)
