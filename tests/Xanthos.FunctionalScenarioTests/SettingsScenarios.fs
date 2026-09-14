namespace Xanthos.FunctionalScenarioTests

open System
open System.IO
open Xunit
open Xanthos

module SettingsScenarios =
    let private success =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private verifySameSession (native: NativeFake) =
        use session = new Session(native)

        for flag in [ true; false; true ] do
            JvLink.setSaveFlag flag session |> success
            Assert.Equal((if flag then 1 else 0), JvLink.getSaveFlag session |> success)

        for path in
            [ Path.Combine(Path.GetTempPath(), "Xanthos-A")
              Path.Combine(Path.GetTempPath(), "Xanthos-B") ] do
            JvLink.setSavePath path session |> success
            Assert.Equal(path, JvLink.getSavePath session |> success)

    [<Fact>]
    let ``Q04 same session flag and full absolute path roundtrip`` () =
        verifySameSession (new NativeFake(SettingsStore()))

    [<Theory>]
    [<InlineData("JVSetSaveFlag")>]
    [<InlineData("JVSetSavePath")>]
    let ``Q04 ignored setter is detected by the same roundtrip assertions`` ignored =
        let native = new NativeFake(SettingsStore())
        native.Handler <- fun api _ -> if api = ignored then Some(Ok(box 0)) else None

        Assert.ThrowsAny<Xunit.Sdk.XunitException>(fun () -> verifySameSession native)
        |> ignore

        Assert.Equal(1, native.Disposals)

    [<Fact>]
    let ``Q04 fresh CLI sessions share only the explicitly owned settings store`` () =
        let store = SettingsStore()

        let invoke args =
            let native = new NativeFake(store)
            let code, text = Host.run native args
            Assert.True((code = 0), text)
            Assert.Equal(1, native.Disposals)
            text

        for flag in [ "true"; "false"; "true" ] do
            invoke [| "set-save-flag"; "--value"; flag |] |> ignore
            Assert.Contains($"Save flag: {flag}\n", invoke [| "get-save-flag" |] + "\n")

        for path in
            [ Path.Combine(Path.GetTempPath(), "Xanthos-A")
              Path.Combine(Path.GetTempPath(), "Xanthos-B") ] do
            invoke [| "set-save-path"; "--value"; path |] |> ignore
            Assert.Contains($"Save path: {path}\n", invoke [| "get-save-path" |] + "\n")

        let other = SettingsStore()
        Assert.Equal("initial", other.SavePath)
        Assert.Equal(1, other.SaveFlag)
