module SettingsRoundtrip

open System

type Snapshot = { Flag: int; Path: string }

type Operations =
    { Snapshot: unit -> Snapshot
      SetPath: string -> unit
      SetFlag: bool -> unit
      SamePath: string -> string -> bool
      VerifyIsolated: unit -> unit
      VerifyOriginalData: unit -> unit
      Report: string -> unit }

/// Every operation must use a fresh native session. Never restore the original
/// path while the saving flag is different or its current value is unknown.
let run operations isolatedPath inject =
    let original = operations.Snapshot()

    if original.Flag <> 0 && original.Flag <> 1 then
        failwith "Unexpected original save flag. Settings were not changed."

    if operations.SamePath original.Path isolatedPath then
        failwith "Verification requires a separate empty save path."

    operations.VerifyIsolated()
    let mutable bodyError: exn option = None
    let injectedError = InvalidOperationException("controlled-after-change")

    try
        operations.SetPath isolatedPath
        let isolated = operations.Snapshot()

        if
            not (operations.SamePath isolated.Path isolatedPath)
            || isolated.Flag <> original.Flag
        then
            failwith "Fresh-session isolation verification failed."

        operations.VerifyIsolated()
        operations.Report "SETTINGS isolatedPathMatched=true isolatedDirectoryEmpty=true"
        operations.SetFlag(original.Flag = 0)
        let changed = operations.Snapshot()

        if
            changed.Flag <> 1 - original.Flag
            || not (operations.SamePath changed.Path isolatedPath)
        then
            failwith "Fresh-session changed settings differ."

        operations.Report "SETTINGS changedMatched=true"

        if inject then
            raise injectedError
    with error ->
        bodyError <- Some error

    try
        let current = operations.Snapshot()

        if current.Flag <> original.Flag then
            if not (operations.SamePath current.Path isolatedPath) then
                failwith "Save path is not isolated; refusing flag restoration."

            operations.VerifyIsolated()
            operations.SetFlag(original.Flag <> 0)
            let restoredFlag = operations.Snapshot()

            if
                restoredFlag.Flag <> original.Flag
                || not (operations.SamePath restoredFlag.Path isolatedPath)
            then
                failwith "Save flag restoration was not verified; retaining the isolated path."

        if not (operations.SamePath (operations.Snapshot()).Path original.Path) then
            operations.SetPath original.Path

        let restored = operations.Snapshot()

        if
            restored.Flag <> original.Flag
            || not (operations.SamePath restored.Path original.Path)
        then
            failwith "SDK settings restoration failed. Human recovery is required."

        operations.VerifyOriginalData()
        operations.Report "SETTINGS restoredMatched=true originalDataUnchanged=true"
    with cleanupError ->
        let errors =
            match bodyError with
            | Some error -> [| error; cleanupError |]
            | None -> [| cleanupError |]

        raise (AggregateException("Settings recovery or data verification failed. Human recovery is required.", errors))

    match bodyError with
    | Some error when Object.ReferenceEquals(error, injectedError) -> ()
    | Some error -> System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw()
    | None -> ()

    operations.Report(sprintf "SETTINGS completed=true injected=%b" inject)
