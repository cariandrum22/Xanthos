module Xanthos.Cli.Types

open System
open Xanthos.Interop
open Xanthos.Runtime

type StubPreference =
    | PreferCom
    | ForcedByUser
    | ForcedByPlatform

type ClientMode =
    | Com
    | Stub of string

/// Describes how the client will be activated.
/// Note: The actual client is created per-service call to ensure proper ownership semantics.
type ClientActivation = { Mode: ClientMode; FallbackCom: bool }

type GlobalRawOptions =
    { Sid: string option
      ServiceKey: string option
      SavePath: string option
      ForceStub: bool
      EnableDiagnostics: bool
      ShowHelp: bool
      UseJvGets: bool option }

type GlobalSettings =
    {
        Sid: string
        ServiceKey: string option
        SavePath: string option
        StubPreference: StubPreference
        EnableDiagnostics: bool
        /// When Some true, use JVGets (byte array) instead of JVRead (BSTR).
        /// When None, falls back to XANTHOS_USE_JVGETS environment variable.
        UseJvGets: bool option
    }

type DownloadArgs =
    { Request: JvOpenRequest
      OutputDirectory: string option }

type RealtimeRaw =
    { RealtimeSpec: string option
      RealtimeKey: string option
      Continuous: bool }

/// Arguments for realtime data requests.
/// Key format: "YYYYMMDDJJKKHHRR" (race), "YYYYMMDD" (daily), or WatchEvent parameter.
type RealtimeArgs =
    {
        Spec: string
        Key: string
        /// When true, continuously poll until Ctrl+C. When false, exit after EndOfStream.
        Continuous: bool
    }

type WatchRaw =
    { DurationText: string option
      OpenAfter: bool }

type WatchArgs =
    { Duration: TimeSpan option
      OpenAfterRealtime: bool }

type SilksFileArgs = { Pattern: string; OutputPath: string }

type CourseFile2Args = { Key: string; OutputPath: string }

type MovieWithTypeArgs =
    { MovieTypeCode: string
      MovieKey: string }

type MovieOpenArgs =
    { MovieOpenType: string
      MovieSearchKey: string }

type CaptureFixturesArgs =
    { FixturesOutputDir: string
      Specs: string list
      FromTime: DateTime
      ToTime: DateTime option
      MaxRecordsPerType: int
      UseJvGets: bool }

type Command =
    | Help
    | Download of DownloadArgs
    | Realtime of RealtimeArgs
    | Status
    | Skip
    | Cancel
    | DeleteFile of string
    | WatchEvents of WatchArgs
    | SetSaveFlag of bool
    | GetSaveFlag
    | SetSavePath of string
    | GetSavePath
    | SetServiceKey of string
    | GetServiceKey
    | CourseFile of string
    | CourseFile2 of CourseFile2Args
    | SilksFile of SilksFileArgs
    | SilksBinary of string
    | MovieCheck of string
    | MovieCheckWithType of MovieWithTypeArgs
    | MoviePlay of string
    | MoviePlayWithType of MovieWithTypeArgs
    | MovieOpen of MovieOpenArgs
    | SetUiProperties
    | Version
    | TotalReadSize
    | CurrentReadSize
    | CurrentFileTimestamp
    | SetParentHwnd of nativeint
    | GetParentHwnd
    | SetPayoffDialog of bool
    | GetPayoffDialog
    | CaptureFixtures of CaptureFixturesArgs

type ParsedInput =
    { Globals: GlobalSettings
      Command: Command }

type ResultBuilder() =
    member _.Bind(v, f) = Result.bind f v
    member _.Return x = Ok x
    member _.ReturnFrom r = r

let result = ResultBuilder()
