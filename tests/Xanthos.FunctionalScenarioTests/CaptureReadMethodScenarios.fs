namespace Xanthos.FunctionalScenarioTests

open System
open System.IO
open System.Text.Json
open Xunit

module CaptureReadMethodScenarios =
    [<Fact>]
    let ``Capture retains malformed bytes and diagnostics then continues records and dataspecs`` () =
        let directory =
            Path.Combine(Path.GetTempPath(), "Xanthos-Capture-" + Guid.NewGuid().ToString("N"))

        let native = new NativeFake(SettingsStore())
        let bad = [| byte 'W'; byte 'H'; byte '1' |]
        let valid = Host.record "WH"
        let unknown = [| byte 'Z'; byte 'Z' |]
        let mutable spec = ""
        let mutable index = 0

        native.Handler <-
            fun api args ->
                if api = "JVOpen" then
                    spec <- unbox args[0]
                    index <- 0
                    None
                elif api = "JVGets" then
                    let records = if spec = "RACE" then [| bad; valid |] else [| unknown |]

                    if index = records.Length then
                        Some(Ok(box 0))
                    else
                        let bytes = records[index]
                        index <- index + 1
                        args[0] <- box bytes
                        args[2] <- box "controlled.jvd"
                        Some(Ok(box bytes.Length))
                else
                    None

        try
            let code, output =
                Host.run
                    native
                    [| "capture-fixtures"
                       "--use-jvgets"
                       "--output"
                       directory
                       "--specs"
                       "RACE,TOKU"
                       "--from"
                       "20260905000000"
                       "--max-records"
                       "2" |]

            Assert.Equal(2, code)
            Assert.Contains("Retained 1 records with parse errors", output)
            Assert.Equal(3, Directory.GetFiles(directory, "*.bin").Length)

            for name, bytes, prefix in
                [ "RACE_WH_001", bad, "error: WH.RecordLength"
                  "RACE_WH_002", valid, "ok"
                  "TOKU_ZZ_001", unknown, "ok" ] do
                Assert.Equal<byte>(bytes, File.ReadAllBytes(Path.Combine(directory, name + ".bin")))

                use metadata =
                    JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, name + ".meta.json")))

                Assert.StartsWith(prefix, metadata.RootElement.GetProperty("parseStatus").GetString())

                Assert.Equal(
                    Convert.ToHexString(Security.Cryptography.SHA256.HashData bytes),
                    metadata.RootElement.GetProperty("sha256").GetString()
                )

                Assert.Equal("controlled.jvd", metadata.RootElement.GetProperty("sourceFile").GetString())

            Assert.Equal(2, native.Calls |> Array.filter ((=) "JVClose") |> Array.length)
            Assert.Equal(1, native.Disposals)
        finally
            if Directory.Exists directory then
                Directory.Delete(directory, true)

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
