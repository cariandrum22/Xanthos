namespace Xanthos.Data

open Xanthos

/// The eight betting products represented by standard payoff and vote records.
type BetValues<'T> =
    { Win: 'T
      Place: 'T
      BracketQuinella: 'T
      Quinella: 'T
      Wide: 'T
      Exacta: 'T
      Trio: 'T
      Trifecta: 'T }

type Payout =
    { Combination: string
      AmountYen: Sourced<decimal option>
      Popularity: Sourced<int option> }

type Payoff =
    { Header: RecordHeader
      Identity: RaceIdentity
      RegisteredCount: Sourced<int option>
      RunnerCount: Sourced<int option>
      Unformed: BetValues<Sourced<bool option>>
      SpecialPayout: BetValues<Sourced<bool option>>
      Refunded: BetValues<Sourced<bool option>>
      RefundedHorses: Sourced<bool option> array
      RefundedBrackets: Sourced<bool option> array
      RefundedSameBrackets: Sourced<bool option> array
      Payouts: BetValues<Payout array>
      Raw: byte[] }

module internal PayoffParser =
    let parse (data: byte[]) =
        Reader.parse
            "HR"
            719
            [ "0"; "1"; "2"; "9" ]
            (fun reader header ->
                let flag name position =
                    let raw = reader.Ascii name position 1

                    { Raw = raw
                      Value =
                        match raw with
                        | "0" -> Some false
                        | "1" -> Some true
                        | _ -> None }

                let flags name position =
                    { Win = flag (name + ".Win") position
                      Place = flag (name + ".Place") (position + 1)
                      BracketQuinella = flag (name + ".BracketQuinella") (position + 2)
                      Quinella = flag (name + ".Quinella") (position + 3)
                      Wide = flag (name + ".Wide") (position + 4)
                      Exacta = flag (name + ".Exacta") (position + 6)
                      Trio = flag (name + ".Trio") (position + 7)
                      Trifecta = flag (name + ".Trifecta") (position + 8) }

                let payouts name position width count combinationWidth popularityWidth =
                    Array.init count (fun i ->
                        let start = position + i * width
                        let field property = $"Payouts.{name}[{i}].{property}"

                        { Combination = reader.Identifier true (field "Combination") start combinationWidth
                          AmountYen = reader.Number (field "AmountYen") (start + combinationWidth) 9 1M
                          Popularity = reader.Int (field "Popularity") (start + combinationWidth + 9) popularityWidth })

                { Header = header
                  Identity = reader.Identity 12
                  RegisteredCount = reader.Int "RegisteredCount" 28 2
                  RunnerCount = reader.Int "RunnerCount" 30 2
                  Unformed = flags "Unformed" 32
                  SpecialPayout = flags "SpecialPayout" 41
                  Refunded = flags "Refunded" 50
                  RefundedHorses = Array.init 28 (fun i -> flag ($"RefundedHorses[{i}]") (59 + i))
                  RefundedBrackets = Array.init 8 (fun i -> flag ($"RefundedBrackets[{i}]") (87 + i))
                  RefundedSameBrackets = Array.init 8 (fun i -> flag ($"RefundedSameBrackets[{i}]") (95 + i))
                  Payouts =
                    { Win = payouts "Win" 103 13 3 2 2
                      Place = payouts "Place" 142 13 5 2 2
                      BracketQuinella = payouts "BracketQuinella" 207 13 3 2 2
                      Quinella = payouts "Quinella" 246 16 3 4 3
                      Wide = payouts "Wide" 294 16 7 4 3
                      Exacta = payouts "Exacta" 454 16 6 4 3
                      Trio = payouts "Trio" 550 18 3 6 3
                      Trifecta = payouts "Trifecta" 604 19 6 6 4 }
                  Raw = Array.copy data })
            data
