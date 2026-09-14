namespace Xanthos.Data

open Xanthos

type VoteEntry =
    { Combination: string
      UnitsHundredYen: Sourced<decimal option>
      Popularity: Sourced<Popularity> }

type Votes =
    { Header: RecordHeader
      Identity: RaceIdentity
      RegisteredCount: Sourced<int option>
      RunnerCount: Sourced<int option>
      Sales: SevenBetValues<Sourced<SaleState>>
      PlacePayoutRule: Sourced<PlacePayoutRule>
      RefundedHorses: Sourced<bool option> array
      RefundedBrackets: Sourced<bool option> array
      RefundedSameBrackets: Sourced<bool option> array
      Entries: SevenBetValues<VoteEntry array>
      TotalUnitsHundredYen: SevenBetValues<Sourced<decimal option>>
      RefundedUnitsHundredYen: SevenBetValues<Sourced<decimal option>>
      Raw: byte[] }

type TrifectaVotes =
    { Header: RecordHeader
      Identity: RaceIdentity
      RegisteredCount: Sourced<int option>
      RunnerCount: Sourced<int option>
      Sale: Sourced<SaleState>
      RefundedHorses: Sourced<bool option> array
      Entries: VoteEntry array
      TotalUnitsHundredYen: Sourced<decimal option>
      RefundedUnitsHundredYen: Sourced<decimal option>
      Raw: byte[] }

module internal VotesParser =
    let private entries (reader: Reader) name position width count combinationWidth popularityWidth =
        Array.init count (fun i ->
            let start = position + width * i
            let field property = $"{name}[{i}].{property}"

            { Combination = reader.Identifier true (field "Combination") start combinationWidth
              UnitsHundredYen = reader.Number (field "UnitsHundredYen") (start + combinationWidth) 11 1M
              Popularity =
                BettingReader.popularity reader (field "Popularity") (start + combinationWidth + 11) popularityWidth })

    let parseH1 (data: byte[]) =
        Reader.parse
            "H1"
            28955
            [ "0"; "2"; "4"; "5"; "9" ]
            (fun reader header ->
                { Header = header
                  Identity = reader.Identity 12
                  RegisteredCount = reader.Int "RegisteredCount" 28 2
                  RunnerCount = reader.Int "RunnerCount" 30 2
                  Sales = BettingReader.seven (fun name i -> BettingReader.sale reader ("Sales." + name) (32 + i))
                  PlacePayoutRule = BettingReader.placeRule reader "PlacePayoutRule" 39
                  RefundedHorses = Array.init 28 (fun i -> BettingReader.flag reader ($"RefundedHorses[{i}]") (40 + i))
                  RefundedBrackets =
                    Array.init 8 (fun i -> BettingReader.flag reader ($"RefundedBrackets[{i}]") (68 + i))
                  RefundedSameBrackets =
                    Array.init 8 (fun i -> BettingReader.flag reader ($"RefundedSameBrackets[{i}]") (76 + i))
                  Entries =
                    { Win = entries reader "Entries.Win" 84 15 28 2 2
                      Place = entries reader "Entries.Place" 504 15 28 2 2
                      BracketQuinella = entries reader "Entries.BracketQuinella" 924 15 36 2 2
                      Quinella = entries reader "Entries.Quinella" 1464 18 153 4 3
                      Wide = entries reader "Entries.Wide" 4218 18 153 4 3
                      Exacta = entries reader "Entries.Exacta" 6972 18 306 4 3
                      Trio = entries reader "Entries.Trio" 12480 20 816 6 3 }
                  TotalUnitsHundredYen =
                    BettingReader.seven (fun name i ->
                        reader.Number ("TotalUnitsHundredYen." + name) (28800 + i * 11) 11 1M)
                  RefundedUnitsHundredYen =
                    BettingReader.seven (fun name i ->
                        reader.Number ("RefundedUnitsHundredYen." + name) (28877 + i * 11) 11 1M)
                  Raw = Array.copy data })
            data

    let parseH6 (data: byte[]) =
        Reader.parse
            "H6"
            102890
            [ "0"; "2"; "4"; "5"; "9" ]
            (fun reader header ->
                { Header = header
                  Identity = reader.Identity 12
                  RegisteredCount = reader.Int "RegisteredCount" 28 2
                  RunnerCount = reader.Int "RunnerCount" 30 2
                  Sale = BettingReader.sale reader "Sale" 32
                  RefundedHorses = Array.init 18 (fun i -> BettingReader.flag reader ($"RefundedHorses[{i}]") (33 + i))
                  Entries = entries reader "Entries" 51 21 4896 6 4
                  TotalUnitsHundredYen = reader.Number "TotalUnitsHundredYen" 102867 11 1M
                  RefundedUnitsHundredYen = reader.Number "RefundedUnitsHundredYen" 102878 11 1M
                  Raw = Array.copy data })
            data
