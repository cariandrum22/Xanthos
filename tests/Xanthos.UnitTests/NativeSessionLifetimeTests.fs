namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos
open Xanthos.Interop

module NativeSessionLifetimeTests =
    let private cleanup (lifetime: NativeSessionLifetime) reply =
        let calls = ResizeArray<string>()

        let result =
            lifetime.Cleanup(
                (fun api ->
                    calls.Add api
                    reply api),
                (fun () -> calls.Add "detach"),
                (fun () -> calls.Add "release")
            )

        result, List.ofSeq calls

    [<Fact; Trait("Category", "Contract")>]
    let ``Initialization and failed open do not schedule a data close`` () =
        let lifetime = NativeSessionLifetime()
        lifetime.Observe("JVInit", 0)
        lifetime.Observe("JVOpen", -305)

        let result, calls =
            cleanup lifetime (fun _ -> failwith "No data session was opened")

        Assert.Equal(Ok(), result)
        Assert.Equal<string list>([ "detach"; "release" ], calls)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("JVOpen", 0); InlineData("JVOpen", -1)>]
    [<InlineData("JVRTOpen", 0); InlineData("JVRTOpen", -1)>]
    [<InlineData("JVOpen", -2); InlineData("JVRTOpen", -2)>]
    [<InlineData("JVMVOpen", 0); InlineData("JVMVOpen", -1)>]
    let ``An owned data or movie session is closed before releasing COM`` api code =
        let lifetime = NativeSessionLifetime()
        lifetime.Observe(api, code)
        let result, calls = cleanup lifetime (fun _ -> Ok 0)
        Assert.Equal(Ok(), result)
        Assert.Equal<string list>([ "detach"; "JVClose"; "release" ], calls)

    [<Fact; Trait("Category", "Contract")>]
    let ``Closed data followed by watch start and stop must not repeat JVClose`` () =
        let lifetime = NativeSessionLifetime()

        for api in [ "JVInit"; "JVOpen"; "JVClose"; "JVWatchEvent"; "JVWatchEventClose" ] do
            lifetime.Observe(api, 0)

        let result, calls =
            cleanup lifetime (fun _ -> failwith "A redundant close would hang the SDK")

        Assert.Equal(Ok(), result)
        Assert.Equal<string list>([ "detach"; "release" ], calls)

    [<Fact; Trait("Category", "Contract")>]
    let ``Active watch ownership is stopped independently of closed data`` () =
        let lifetime = NativeSessionLifetime()

        for api in [ "JVOpen"; "JVClose"; "JVWatchEvent" ] do
            lifetime.Observe(api, 0)

        let result, calls = cleanup lifetime (fun _ -> Ok 0)
        Assert.Equal(Ok(), result)
        Assert.Equal<string list>([ "JVWatchEventClose"; "detach"; "release" ], calls)

    [<Fact; Trait("Category", "Contract")>]
    let ``Reopening after a successful close restores data ownership`` () =
        let lifetime = NativeSessionLifetime()

        for api in [ "JVOpen"; "JVClose"; "JVRTOpen" ] do
            lifetime.Observe(api, 0)
        // An unsuccessful second open does not erase the previously acquired resource.
        lifetime.Observe("JVOpen", -201)
        let result, calls = cleanup lifetime (fun _ -> Ok 0)
        Assert.Equal(Ok(), result)
        Assert.Contains("JVClose", calls)

    [<Fact; Trait("Category", "Contract")>]
    let ``Failed explicit close retains ownership and cleanup failure is not success`` () =
        let lifetime = NativeSessionLifetime()
        lifetime.Observe("JVOpen", 0)
        lifetime.Observe("JVClose", -999)
        let result, calls = cleanup lifetime (fun _ -> Ok -999)

        match result with
        | Error error ->
            Assert.Equal("JVClose", error.Api)
            Assert.Equal(Some -999, error.Code)
        | Ok() -> failwith "Failed close became success"

        Assert.Equal<string list>([ "detach"; "JVClose"; "release" ], calls)

    [<Fact; Trait("Category", "Contract")>]
    let ``Watch failure does not prevent data cleanup or reference release`` () =
        let lifetime = NativeSessionLifetime()
        lifetime.Observe("JVOpen", 0)
        lifetime.Observe("JVWatchEvent", 0)

        let result, calls =
            cleanup lifetime (fun api -> if api = "JVWatchEventClose" then Ok -1 else Ok 0)

        match result with
        | Error error ->
            Assert.Equal("JVWatchEventClose", error.Api)
            Assert.Equal(Some -1, error.Code)
        | Ok() -> failwith "Failed watch shutdown became success"

        Assert.Equal<string list>([ "JVWatchEventClose"; "detach"; "JVClose"; "release" ], calls)

    [<Fact; Trait("Category", "Contract")>]
    let ``A cleanup exception still releases COM and preserves its HRESULT`` () =
        let lifetime = NativeSessionLifetime()
        lifetime.Observe("JVOpen", 0)

        let result, calls =
            cleanup lifetime (fun _ ->
                raise (
                    Reflection.TargetInvocationException(Runtime.InteropServices.COMException("Injected", -2147467259))
                ))

        match result with
        | Error error ->
            Assert.Equal(Some -2147467259, error.Code)
            Assert.Equal(JvErrorKind.Invocation, error.Kind)
        | Ok() -> failwith "Invocation failure became success"

        Assert.Equal("release", List.last calls)
