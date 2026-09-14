namespace Xanthos.UnitTests

open System
open System.Runtime.InteropServices
open Xunit
open Xanthos.Core
open Xanthos.Interop

module ComArchitectureTests =
    [<Theory>]
    [<InlineData(-2147221164)>] // REGDB_E_CLASSNOTREG
    [<InlineData(-2147024885)>] // HRESULT_FROM_WIN32(ERROR_BAD_FORMAT)
    let ``Activation failure preserves original HRESULT without creating a stub`` (hresult: int) =
        let failure = COMException("Activation failed", hresult)

        match ComClientFactory.tryCreateWithActivator (fun () -> raise failure) with
        | Error fault ->
            Assert.Equal(ComFaultReason.ActivationFailure, fault.Reason)
            Assert.Same(failure, fault.Exception.Value)
            Assert.Equal(hresult, fault.Exception.Value.HResult)
        | Ok _ -> failwith "Activation failure became success."

    [<Fact>]
    let ``Typed activation fault is preserved`` () =
        let expected =
            { Reason = ComFaultReason.MethodResolutionFailure
              Details = "Missing required COM member"
              Exception = None }

        let actual =
            ComClientFactory.tryCreateWithActivator (fun () -> raise (ComActivationException expected))

        match actual with
        | Error fault -> Assert.Equal(expected, fault)
        | Ok _ -> failwith "Typed fault became success."

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(-1)>]
    [<InlineData(Int32.MaxValue)>]
    [<InlineData(Int32.MinValue)>]
    let ``ParentHWnd preserves the signed COM Long range`` (value: int) =
        Assert.Equal(Ok value, ComInterop.windowHandleToInt32 (IntPtr value))

    [<Fact>]
    let ``ParentHWnd rejects high bits instead of truncating them`` () =
        if IntPtr.Size = 8 then
            for value in [ int64 Int32.MaxValue + 1L; int64 Int32.MinValue - 1L; 0x100000001L ] do
                match ComInterop.windowHandleToInt32 (IntPtr value) with
                | Error(InvalidInput _) -> ()
                | other -> failwithf "Expected overflow rejection for %d, got %A" value other
