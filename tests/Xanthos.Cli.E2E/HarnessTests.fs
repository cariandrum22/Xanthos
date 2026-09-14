namespace Xanthos.Cli.E2E

open System
open Xunit

/// Controlled process-output cases, not actual SDK execution.
[<Trait("Category", "Harness")>]
type HarnessTests() =
    let result code text =
        { ExitCode = code
          StdOut = text
          StdErr = ""
          LogFile = "controlled-output" }

    let architecture = "EVIDENCE:ARCH=X64\nEVIDENCE:POINTER_SIZE=8\n"
    let backend = "EVIDENCE:MODE=COM\nEVIDENCE:API=FUNCTIONAL\n"

    [<Theory>]
    [<InlineData("EVIDENCE:MODE=STUB\nEVIDENCE:API=FUNCTIONAL\n")>]
    [<InlineData("EVIDENCE:MODE=COM\nEVIDENCE:MODE=STUB\nEVIDENCE:API=FUNCTIONAL\n")>]
    [<InlineData("EVIDENCE:MODE=COM\n")>]
    [<InlineData("EVIDENCE:API=FUNCTIONAL\n")>]
    member _.``successful COM output cannot hide fallback or missing evidence``(markers: string) =
        Assert.Throws<Exception>(fun () -> Harness.validateEvidence Com (result 0 (architecture + markers)))
        |> ignore

    [<Theory>]
    [<InlineData("EVIDENCE:ARCH=X86\nEVIDENCE:POINTER_SIZE=4\n")>]
    [<InlineData("EVIDENCE:ARCH=X64\n")>]
    [<InlineData("EVIDENCE:ARCH=X64\nEVIDENCE:POINTER_SIZE=8\nEVIDENCE:ARCH=X86\n")>]
    member _.``COM evidence requires consistent x64 architecture even on failure``(markers: string) =
        Assert.Throws<Exception>(fun () -> Harness.validateEvidence Com (result 2 (markers + backend)))
        |> ignore

    [<Fact>]
    member _.``real COM success markers are accepted``() =
        Harness.validateEvidence Com (result 0 (architecture + backend))

    [<Fact>]
    member _.``activation failure may omit backend but cannot switch to stub``() =
        Harness.validateEvidence Com (result 2 (architecture + "COM client creation failed\n"))

        Assert.Throws<Exception>(fun () ->
            Harness.validateEvidence Com (result 2 (architecture + "EVIDENCE:MODE=STUB\n")))
        |> ignore

    [<Fact>]
    member _.``explicit stub cannot advertise COM``() =
        Harness.validateEvidence Stub (result 0 (architecture + "EVIDENCE:MODE=STUB\n"))

        Assert.Throws<Exception>(fun () -> Harness.validateEvidence Stub (result 0 (architecture + backend)))
        |> ignore
