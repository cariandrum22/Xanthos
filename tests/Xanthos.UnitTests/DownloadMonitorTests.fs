module Xanthos.UnitTests.DownloadMonitorTests

open System
open System.Threading
open Xunit
open Xanthos.Runtime
open Xanthos.Core
open Xanthos.Interop

[<Fact>]
let ``waitForCompletion should stop when expected count reached`` () =
    let mutable count = 0

    let status () =
        count <- count + 1
        Ok(min count 3)

    let result =
        DownloadMonitor.waitForCompletion
            status
            3
            (Some(TimeSpan.FromMilliseconds 10.))
            (Some(TimeSpan.FromSeconds 1.))
            (Some CancellationToken.None)

    match result with
    | Ok(Completed completed) -> Assert.Equal(3, completed)
    | Ok(TimedOut _) -> failwith "Unexpected timeout - should have completed"
    | Error e -> failwithf "Unexpected error %A" e

[<Fact>]
let ``waitForCompletion should return TimedOut on timeout`` () =
    let status () = Ok 0

    let result =
        DownloadMonitor.waitForCompletion
            status
            10
            (Some(TimeSpan.FromMilliseconds 10.))
            (Some(TimeSpan.FromMilliseconds 100.))
            (Some CancellationToken.None)

    match result with
    | Ok(TimedOut partialCount) -> Assert.Equal(0, partialCount)
    | Ok(Completed _) -> failwith "Expected timeout, but got completed"
    | Error e -> failwithf "Unexpected error %A" e

[<Fact>]
let ``waitForCompletion should propagate JVStatus error`` () =
    let status () = Error ComError.NotInitialized

    let result =
        DownloadMonitor.waitForCompletion
            status
            1
            (Some(TimeSpan.FromMilliseconds 10.))
            (Some(TimeSpan.FromMilliseconds 50.))
            (Some CancellationToken.None)

    match result with
    | Error NotInitialized -> ()
    | other -> failwithf "Expected NotInitialized, got %A" other
