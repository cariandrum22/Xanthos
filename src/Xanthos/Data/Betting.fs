namespace Xanthos.Data

open System

[<RequireQualifiedAccess>]
type SaleState =
    | NotSold
    | CancelledBeforeSale
    | CancelledAfterSale
    | Available
    | Unknown of string

[<RequireQualifiedAccess>]
type Popularity =
    | NotRegistered
    | CancelledBeforeSale
    | CancelledAfterSale
    | Rank of int

[<RequireQualifiedAccess>]
type PlacePayoutRule =
    | NotSold
    | TwoPlaces
    | ThreePlaces
    | Unknown of string

/// The seven betting products in H1; trifecta data is supplied separately in H6.
type SevenBetValues<'T> =
    { Win: 'T
      Place: 'T
      BracketQuinella: 'T
      Quinella: 'T
      Wide: 'T
      Exacta: 'T
      Trio: 'T }

module internal BettingReader =
    let sale (reader: Reader) name position =
        let raw = reader.Ascii name position 1

        let value =
            match raw with
            | "0" -> SaleState.NotSold
            | "1" -> SaleState.CancelledBeforeSale
            | "3" -> SaleState.CancelledAfterSale
            | "7" -> SaleState.Available
            | _ -> SaleState.Unknown raw

        { Raw = raw; Value = value }

    let placeRule (reader: Reader) name position =
        let raw = reader.Ascii name position 1

        let value =
            match raw with
            | "0" -> PlacePayoutRule.NotSold
            | "2" -> PlacePayoutRule.TwoPlaces
            | "3" -> PlacePayoutRule.ThreePlaces
            | _ -> PlacePayoutRule.Unknown raw

        { Raw = raw; Value = value }

    let popularity (reader: Reader) name position length =
        let raw = reader.Ascii name position length

        let value =
            if raw = String(' ', length) then
                Popularity.NotRegistered
            elif raw = String('-', length) then
                Popularity.CancelledBeforeSale
            elif raw = String('*', length) then
                Popularity.CancelledAfterSale
            else
                match (reader.Int name position length).Value with
                | Some rank -> Popularity.Rank rank
                | None -> reader.Fail name position length "Unknown popularity format."

        { Raw = raw; Value = value }

    let flag (reader: Reader) name position =
        let raw = reader.Ascii name position 1

        { Raw = raw
          Value =
            match raw with
            | "0" -> Some false
            | "1" -> Some true
            | _ -> None }

    let seven operation =
        { Win = operation "Win" 0
          Place = operation "Place" 1
          BracketQuinella = operation "BracketQuinella" 2
          Quinella = operation "Quinella" 3
          Wide = operation "Wide" 4
          Exacta = operation "Exacta" 5
          Trio = operation "Trio" 6 }
