namespace Xanthos.Data

open System
open Xanthos

type TimeMiningEntry =
    { HorseNumber: Sourced<int option>
      PredictedSeconds: Sourced<decimal option>
      FasterErrorSeconds: Sourced<decimal option>
      SlowerErrorSeconds: Sourced<decimal option> }

type TimeMining =
    { Header: RecordHeader
      Identity: RaceIdentity
      CreatedTime: Sourced<TimeOnly option>
      Horses: TimeMiningEntry array
      Raw: byte[] }

type MatchMiningEntry =
    { HorseNumber: Sourced<int option>
      Score: Sourced<decimal option> }

type MatchMining =
    { Header: RecordHeader
      Identity: RaceIdentity
      CreatedTime: Sourced<TimeOnly option>
      Horses: MatchMiningEntry array
      Raw: byte[] }

module internal MiningParser =
    let parseDM data =
        Reader.parse
            "DM"
            303
            [ "0"; "1"; "2"; "3"; "7" ]
            (fun r header ->
                { Header = header
                  Identity = r.Identity 12
                  CreatedTime = r.ClockTime "CreatedTime" 28
                  Horses =
                    Array.init 18 (fun i ->
                        let p = 32 + i * 15
                        let n = $"Horses[{i}]."
                        let time = r.Number (n + "PredictedSeconds") (p + 2) 5 1M

                        let seconds =
                            time.Value
                            |> Option.map (fun _ ->
                                let sec = int time.Raw[1..2]

                                if sec > 59 then
                                    r.Fail (n + "PredictedSeconds") (p + 2) 5 "Invalid mssSS seconds."

                                decimal (int time.Raw[..0] * 60 + sec) + decimal (int time.Raw[3..]) / 100M)

                        { HorseNumber = r.Int (n + "HorseNumber") p 2
                          PredictedSeconds = { Raw = time.Raw; Value = seconds }
                          FasterErrorSeconds = r.Number (n + "FasterErrorSeconds") (p + 7) 4 100M
                          SlowerErrorSeconds = r.Number (n + "SlowerErrorSeconds") (p + 11) 4 100M })
                  Raw = Array.copy data }
                : TimeMining)
            data

    let parseTM data =
        Reader.parse
            "TM"
            141
            [ "0"; "1"; "2"; "3"; "7" ]
            (fun r header ->
                { Header = header
                  Identity = r.Identity 12
                  CreatedTime = r.ClockTime "CreatedTime" 28
                  Horses =
                    Array.init 18 (fun i ->
                        let p = 32 + i * 6
                        let n = $"Horses[{i}]."
                        let score = r.Number (n + "Score") (p + 2) 4 10M

                        if score.Value |> Option.exists (fun v -> v > 100M) then
                            r.Fail (n + "Score") (p + 2) 4 "Score must be between 0.0 and 100.0."

                        { HorseNumber = r.Int (n + "HorseNumber") p 2
                          Score = score })
                  Raw = Array.copy data }
                : MatchMining)
            data
