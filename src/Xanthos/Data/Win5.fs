namespace Xanthos.Data

open System
open Xanthos

type Win5Race =
    { Racecourse: OfficialCode
      Meeting: Sourced<int option>
      Day: Sourced<int option>
      RaceNumber: Sourced<int option> }

type Win5Payout =
    { Combination: string
      AmountYen: Sourced<decimal option>
      WinningTickets: Sourced<decimal option> }

type Win5 =
    { Header: RecordHeader
      Year: Sourced<int option>
      MonthDay: string
      Date: DateOnly option
      Races: Win5Race array
      TicketsSold: Sourced<decimal option>
      RemainingTickets: Sourced<decimal option> array
      Refunded: Sourced<bool option>
      Unformed: Sourced<bool option>
      NoWinners: Sourced<bool option>
      InitialCarryoverYen: Sourced<decimal option>
      NextCarryoverYen: Sourced<decimal option>
      Payouts: Win5Payout array
      Raw: byte[] }

module internal Win5Parser =
    let parse data =
        Reader.parse
            "WF"
            7215
            [ "0"; "1"; "2"; "3"; "7"; "9" ]
            (fun r header ->
                { Header = header
                  Year = r.Int "Year" 12 4
                  MonthDay = r.Digits "MonthDay" 16 4
                  Date = (r.Date "Date" 12).Value
                  Races =
                    Array.init 5 (fun i ->
                        let p = 22 + i * 8
                        let name = $"Races[{i}]"

                        { Racecourse = r.Code CodeTable.Racecourse (name + ".Racecourse") p
                          Meeting = r.Int (name + ".Meeting") (p + 2) 2
                          Day = r.Int (name + ".Day") (p + 4) 2
                          RaceNumber = r.Int (name + ".RaceNumber") (p + 6) 2 })
                  TicketsSold = r.Number "TicketsSold" 68 11 1M
                  RemainingTickets = Array.init 5 (fun i -> r.Number ($"RemainingTickets[{i}]") (79 + i * 11) 11 1M)
                  Refunded = BettingReader.flag r "Refunded" 134
                  Unformed = BettingReader.flag r "Unformed" 135
                  NoWinners = BettingReader.flag r "NoWinners" 136
                  InitialCarryoverYen = r.Number "InitialCarryoverYen" 137 15 1M
                  NextCarryoverYen = r.Number "NextCarryoverYen" 152 15 1M
                  Payouts =
                    Array.init 243 (fun i ->
                        let p = 167 + i * 29
                        let name = $"Payouts[{i}]"

                        { Combination = r.Identifier true (name + ".Combination") p 10
                          AmountYen = r.Number (name + ".AmountYen") (p + 10) 9 1M
                          WinningTickets = r.Number (name + ".WinningTickets") (p + 19) 10 1M })
                  Raw = Array.copy data })
            data
