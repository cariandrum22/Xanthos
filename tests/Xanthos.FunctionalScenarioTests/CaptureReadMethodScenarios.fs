namespace Xanthos.FunctionalScenarioTests

open System
open System.IO
open System.Text.Json
open Xunit

module CaptureReadMethodScenarios =
    [<Theory;
      InlineData("", "", "JVRead");
      InlineData("--use-jvgets", "", "JVGets");
      InlineData("--no-jvgets", "", "JVRead");
      InlineData("", "--use-jvgets", "JVGets");
      InlineData("--use-jvgets", "--no-jvgets", "JVRead");
      InlineData("--no-jvgets", "--use-jvgets", "JVGets")>]
    let ``Capture command read method matches native API bytes and persisted metadata``
        globalFlag
        commandFlag
        expected
        =
        let directory =
            Path.Combine(Path.GetTempPath(), "Xanthos-Capture-" + Guid.NewGuid().ToString("N"))

        let native = new NativeFake(SettingsStore())
        let bytes = Host.record "WH"
        Host.supplyRecord native bytes

        try
            let args =
                [| globalFlag
                   "capture-fixtures"
                   "--output"
                   directory
                   "--specs"
                   "RACE"
                   "--from"
                   "20260905000000"
                   "--max-records"
                   "1"
                   commandFlag |]
                |> Array.filter ((<>) "")

            let code, output = Host.run native args
            Assert.True((code = 0), output)

            let readCalls =
                native.Calls |> Array.filter (fun api -> api = "JVRead" || api = "JVGets")

            Assert.Equal<string>([| expected; expected |], readCalls)
            let file = Assert.Single(Directory.GetFiles(directory, "*.bin"))
            Assert.Equal<byte>(bytes, File.ReadAllBytes file)

            use metadata =
                JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(file, ".meta.json")))

            Assert.Equal(expected, metadata.RootElement.GetProperty("readMethod").GetString())
            Assert.Equal(bytes.Length, metadata.RootElement.GetProperty("byteLength").GetInt32())
            Assert.Equal(1, native.Disposals)
            Assert.Single(native.Calls |> Array.filter ((=) "JVClose")) |> ignore
        finally
            // The unique test-created directory is the only recursive deletion target.
            if Directory.Exists directory then
                Directory.Delete(directory, true)
