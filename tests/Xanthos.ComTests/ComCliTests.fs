namespace Xanthos.ComTests

open System
open Xunit
open Xunit.Abstractions
open Xanthos.Cli.E2E

type ComCliTests(output: ITestOutputHelper) =
    let evidence (result: CliResult) =
        output.WriteLine($"TEST_POINTER_SIZE={IntPtr.Size}; LOG={result.LogFile}")
        output.WriteLine result.StdOut
        output.WriteLine result.StdErr
        Assert.Equal(8, IntPtr.Size)
        Assert.Contains("EVIDENCE:ARCH=X64", result.StdOut)
        Assert.Contains("EVIDENCE:POINTER_SIZE=8", result.StdOut)

    [<Fact; Trait("Category", "ComX64Required")>]
    member _.``version initializes the real x64 COM and closes successfully``() =
        let result = Harness.runCli Com [ "version" ]
        evidence result
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("EVIDENCE:MODE=COM", result.StdOut)
        Assert.Contains("EVIDENCE:API=FUNCTIONAL", result.StdOut)
        Assert.Contains("EVIDENCE:VERSION=0500", result.StdOut)
        Assert.Contains("OK JVInit code=0", result.StdOut)
        // Version initializes COM but never opens a data session.
        Assert.DoesNotContain("DISPOSE JVClose begin", result.StdOut)
        Assert.Contains("COM object released", result.StdOut)
        Assert.Contains("DISPOSE STA shutdown complete", result.StdOut)
        Assert.DoesNotContain("EVIDENCE:MODE=STUB", result.StdOut)
        Assert.DoesNotContain("CALL JVSetServiceKey", result.StdOut)

    [<Theory; Trait("Category", "ComX64Required")>]
    [<InlineData("get-save-flag", "Save flag:")>]
    [<InlineData("get-save-path", "Save path:")>]
    [<InlineData("get-payoff-dialog", "Payoff dialog suppressed:")>]
    [<InlineData("total-read-size", "Total read file size:")>]
    [<InlineData("current-read-size", "Current file size:")>]
    [<InlineData("current-file-timestamp", "Current file timestamp:")>]
    member _.``read-only property uses real x64 COM``(command: string, expected: string) =
        let result = Harness.runCli Com [ command ]
        evidence result
        Assert.Equal(0, result.ExitCode)
        Assert.Contains(expected, result.StdOut)
        Assert.Contains("EVIDENCE:MODE=COM", result.StdOut)
        Assert.DoesNotContain("EVIDENCE:MODE=STUB", result.StdOut)
        Assert.DoesNotContain("CALL JVSetServiceKey", result.StdOut)
        Assert.DoesNotContain("CALL JVSetSavePath", result.StdOut)

    [<Fact; Trait("Category", "ComX64Required")>]
    member _.``course explanation retains Japanese through the owned COM STA``() =
        let result = Harness.runCli Com [ "course-file"; "--key"; "9999999905240011" ]
        evidence result
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("EVIDENCE:API=FUNCTIONAL", result.StdOut)
        Assert.Contains("State=Available", result.StdOut)
        let marker = "Explanation="
        let offset = result.StdOut.IndexOf(marker, StringComparison.Ordinal)
        Assert.True(offset >= 0)
        let explanation = result.StdOut.Substring(offset + marker.Length).Split('\n')[0]

        Assert.True(
            Text.RegularExpressions.Regex.IsMatch(explanation, "[\u3040-\u30FF\u3400-\u9FFF]"),
            "Expected Japanese explanation, not bytes decoded as a Western code page."
        )

    [<Fact; Trait("Category", "ComX64Required")>]
    member _.``both readers preserve real bytes across cancel close and reopen``() =
        let fromTime = Environment.GetEnvironmentVariable "XANTHOS_COM_FROM_TIME"

        Assert.False(
            String.IsNullOrWhiteSpace fromTime,
            "Set XANTHOS_COM_FROM_TIME to an available RACE delivery interval (yyyyMMddHHmmss)."
        )

        let directory =
            IO.Path.Combine(Harness.savePath, "com-records", Guid.NewGuid().ToString("N"))

        let records =
            [ "--use-jvgets"; "--no-jvgets" ]
            |> List.map (fun reader ->
                let destination = IO.Path.Combine(directory, reader.TrimStart('-'))

                let result =
                    Harness.runCli
                        Com
                        [ reader
                          "session-check"
                          "--spec"
                          "RACE"
                          "--from"
                          fromTime
                          "--max-records"
                          "1"
                          "--output"
                          destination ]

                evidence result
                Assert.Equal(0, result.ExitCode)
                Assert.Contains("EVIDENCE:API=FUNCTIONAL", result.StdOut)
                Assert.DoesNotContain("EVIDENCE:MODE=STUB", result.StdOut)

                for marker in
                    [ "STATUS completed="
                      "parsed=true"
                      "SKIP succeeded"
                      "CANCEL succeeded"
                      "CLOSE succeeded"
                      "REOPEN succeeded"
                      "DISPOSE STA shutdown complete" ] do
                    Assert.Contains(marker, result.StdOut)

                let path = Assert.Single(IO.Directory.GetFiles(destination, "*.bin"))
                let bytes = IO.File.ReadAllBytes path
                Assert.NotEmpty bytes
                bytes)

        Assert.Equal<byte>(records.[0], records.[1])

    [<Fact; Trait("Category", "ComX64Required")>]
    member _.``No Image remains distinct from successful image generation``() =
        let result = Harness.runCli Com [ "silks-binary"; "--pattern"; "存在しない模様" ]
        evidence result
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("State=NoImage", result.StdOut)
        Assert.Contains("OK JVFuku code=-1", result.StdOut)

    [<Fact; Trait("Category", "ComX64Required")>]
    member _.``subscription starts and releases without requiring a live notification``() =
        let result = Harness.runCli Com [ "watch-events"; "--duration"; "1" ]
        evidence result
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("Watch stopped.", result.StdOut)
        Assert.Contains("DISPOSE STA shutdown complete", result.StdOut)

    [<Fact; Trait("Category", "ComX64Negative")>]
    member _.``non-interactive command requests desktop setup without opening a dialog``() =
        let result = Harness.runCli Com [ "--non-interactive"; "set-ui-properties" ]
        evidence result
        Assert.Equal(2, result.ExitCode)
        Assert.Contains("signed-in desktop", result.StdOut)
        Assert.DoesNotContain("CALL JVSetUIProperties", result.StdOut)
        Assert.DoesNotContain("EVIDENCE:MODE=STUB", result.StdOut)

    [<Fact; Trait("Category", "ComX64Negative")>]
    member _.``x86 executable is rejected before any CLI launch``() =
        let windows = Environment.GetFolderPath Environment.SpecialFolder.Windows

        let x86 =
            IO.Path.Combine(windows, "SysWOW64", "WindowsPowerShell", "v1.0", "powershell.exe")

        let error =
            Assert.Throws<Exception>(fun () -> Harness.validateExecutable x86 |> ignore)

        Assert.Contains("AMD64", error.Message)

    [<Fact; Trait("Category", "ComX64Negative")>]
    member _.``explicit COM activation failure exits without a stub``() =
        let result =
            Harness.runCliWithEnvironment
                Com
                [ "XANTHOS_COM_PROGID", "Xanthos.Unregistered.TestComponent" ]
                [ "version" ]

        evidence result
        Assert.Equal(2, result.ExitCode)
        Assert.Contains("COM client creation failed", result.StdOut)
        Assert.DoesNotContain("EVIDENCE:MODE=STUB", result.StdOut)

    [<Fact; Trait("Category", "ComX64Negative")>]
    member _.``explicit stub ignores an unavailable COM component``() =
        let result =
            Harness.runCliWithEnvironment
                Stub
                [ "XANTHOS_COM_PROGID", "Xanthos.Unregistered.TestComponent" ]
                [ "version" ]

        evidence result
        Assert.Equal(0, result.ExitCode)
        Assert.Contains("EVIDENCE:MODE=STUB", result.StdOut)
        Assert.DoesNotContain("EVIDENCE:MODE=COM", result.StdOut)
