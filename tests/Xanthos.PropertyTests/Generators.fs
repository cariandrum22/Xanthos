module Xanthos.PropertyTests.Generators

open System
open FsCheck
open FsCheck.FSharp
open Xanthos.Core

let private defaults = ArbMap.defaults

let private trimmedStringGenWith invalidChars maxLength : Gen<string> =
    ArbMap.generate<NonEmptyString> defaults
    |> Gen.map (fun (NonEmptyString s) ->
        s
        |> Seq.filter (fun ch -> not (Char.IsControl ch) && not (List.contains ch invalidChars))
        |> Seq.toArray
        |> fun chars -> new string (chars)
        |> fun value -> value.Trim [| ' '; '\t'; '\u00A0'; '\r'; '\n' |])
    |> Gen.filter (fun s -> not (String.IsNullOrWhiteSpace s))
    |> fun gen ->
        match maxLength with
        | Some limit -> Gen.filter (fun s -> s.Length <= limit) gen
        | None -> gen

let private trimmedStringGen = trimmedStringGenWith [ '/'; '\\'; '|' ]

let private optionalStringGen maxLength =
    Gen.frequency [ 1, Gen.constant None; 3, trimmedStringGen maxLength |> Gen.map Some ]

let private nonNegativeDecimalGen: Gen<decimal> =
    let decimalGen = ArbMap.generate<decimal> defaults |> Gen.map Decimal.Abs
    decimalGen

let private decimalOptionGen: Gen<decimal option> =
    Gen.frequency [ 3, Gen.constant None; 7, nonNegativeDecimalGen |> Gen.map Some ]

let private timestampGen: Gen<DateTime> =
    ArbMap.generate<DateTime> defaults
    |> Gen.map (fun dt -> DateTime.SpecifyKind(dt, DateTimeKind.Utc))

let private byteArrayGen: Gen<byte[]> =
    Gen.choose (0, 64)
    |> Gen.bind (fun len ->
        Gen.listOfLength len (Gen.choose (0, 255))
        |> Gen.map (List.map byte)
        |> Gen.map List.toArray)

let private distanceOptionGen: Gen<int option> =
    Gen.frequency [ 1, Gen.constant None; 3, Gen.choose (100, 4800) |> Gen.map Some ]

let private surfaceGen =
    Gen.elements
        [ TrackSurface.Turf
          TrackSurface.Dirt
          TrackSurface.Synthetic
          TrackSurface.UnknownSurface ]

let private conditionGen =
    Gen.elements
        [ TrackCondition.Fast
          TrackCondition.Good
          TrackCondition.Yielding
          TrackCondition.Soft
          TrackCondition.Heavy
          TrackCondition.UnknownCondition ]

let private scheduledStartGen: Gen<DateTime option> =
    Gen.frequency [ 2, Gen.constant None; 3, timestampGen |> Gen.map Some ]

/// Generate a valid RaceId with date prefix (yyyyMMdd) + optional alphanumeric suffix
let raceIdGen =
    gen {
        let! year = Gen.choose (2000, 2030)
        let! month = Gen.choose (1, 12)
        let! day = Gen.choose (1, 28) // Simplified to avoid invalid dates
        let datePrefix = sprintf "%04d%02d%02d" year month day
        let! suffixLen = Gen.choose (0, 8)

        let! suffix =
            Gen.listOfLength suffixLen (Gen.elements ([ 'A' .. 'Z' ] @ [ '0' .. '9' ]))
            |> Gen.map (fun chars -> String(chars |> List.toArray))

        return RaceId.unsafe (datePrefix + suffix)
    }

let raceIdArb = Arb.fromGen raceIdGen

/// Generate a valid RunnerId (exactly 10 digits - blood registration number format)
let runnerIdGen =
    gen {
        let! digits = Gen.listOfLength 10 (Gen.choose (0, 9))
        let value = digits |> List.map string |> String.concat ""
        return RunnerId.unsafe value
    }

let runnerIdArb = Arb.fromGen runnerIdGen

let raceInfoGen =
    gen {
        let! raceId = raceIdGen
        let! name = trimmedStringGenWith [ '/'; '\\'; ':'; '*'; '?'; '"'; '<'; '>'; '|' ] (Some 64)
        let! course = optionalStringGen (Some 32)
        let! distance = distanceOptionGen
        let! surface = surfaceGen
        let! condition = conditionGen
        let! scheduled = scheduledStartGen

        return
            { Id = raceId
              Name = name
              Course = course
              DistanceMeters = distance
              Surface = surface
              Condition = condition
              ScheduledStart = scheduled }
    }

let raceInfoArb = Arb.fromGen raceInfoGen
let raceInfoListArb = raceInfoGen |> Gen.listOf |> Arb.fromGen

let runnerOddsGen =
    gen {
        let! runner = runnerIdGen
        let! includeWin = Gen.elements [ true; false ]

        let! includePlace =
            if includeWin then
                Gen.elements [ true; false ]
            else
                Gen.constant true

        let! winValue = nonNegativeDecimalGen
        let! placeValue = nonNegativeDecimalGen

        let win = if includeWin then Some winValue else None
        let place = if includePlace then Some placeValue else None

        return
            { Runner = runner
              WinOdds = win
              PlaceOdds = place }
    }

let runnerOddsArb = Arb.fromGen runnerOddsGen

let private uniqueRunnerIdsGen length =
    let rec loop () =
        gen {
            let! ids = Gen.listOfLength length runnerIdGen

            if (ids |> List.distinct |> List.length) = length then
                return ids
            else
                return! loop ()
        }

    loop ()

let runnerOddsListGen =
    gen {
        let! count = Gen.choose (1, 24)
        let! runnerIds = uniqueRunnerIdsGen count
        let! winOdds = Gen.listOfLength count decimalOptionGen
        let! placeOdds = Gen.listOfLength count decimalOptionGen

        return
            (runnerIds, winOdds, placeOdds)
            |||> List.map3 (fun runner win place ->
                { Runner = runner
                  WinOdds = win
                  PlaceOdds = place })
    }

let raceOddsGen =
    Gen.map3
        (fun race timestamp entries ->
            { Race = race
              Timestamp = timestamp
              Entries = entries })
        raceIdGen
        timestampGen
        runnerOddsListGen

let raceOddsArb = Arb.fromGen raceOddsGen

let raceOddsListArb = raceOddsGen |> Gen.listOf |> Arb.fromGen

let payloadBatchArb = byteArrayGen |> Gen.listOf |> Arb.fromGen

type CustomArbitraries =
    static member RaceId() = raceIdArb
    static member RunnerId() = runnerIdArb
    static member RaceInfo() = raceInfoArb
    static member RaceInfoList() = raceInfoListArb
    static member RunnerOdds() = runnerOddsArb
    static member RaceOdds() = raceOddsArb
    static member RaceOddsList() = raceOddsListArb
    static member PayloadBatches() = payloadBatchArb
