namespace Xanthos

/// Meanings from the official format list; H5 is not an official record identifier.
[<RequireQualifiedAccess>]
type RecordKind =
    | SpecialRegistration
    | Race
    | Runner
    | Payoff
    | Votes
    | TrifectaVotes
    | WinPlaceBracketOdds
    | QuinellaOdds
    | WideOdds
    | ExactaOdds
    | TrioOdds
    | TrifectaOdds
    | Horse
    | Jockey
    | Trainer
    | Breeder
    | Owner
    | BreedingHorse
    | Offspring
    | RaceFrequency
    | RaceRecord
    | HillTraining
    | MarketPrice
    | HorseNameOrigin
    | Schedule
    | Lineage
    | Course
    | TimeMining
    | MatchMining
    | Win5
    | Exclusion
    | WoodchipTraining
    | HorseWeight
    | Weather
    | Withdrawal
    | JockeyChange
    | TimeChange
    | CourseChange
    | Unknown of string

[<RequireQualifiedAccess>]
module RecordKinds =
    let ofId =
        function
        | "TK" -> RecordKind.SpecialRegistration
        | "RA" -> RecordKind.Race
        | "SE" -> RecordKind.Runner
        | "HR" -> RecordKind.Payoff
        | "H1" -> RecordKind.Votes
        | "H6" -> RecordKind.TrifectaVotes
        | "O1" -> RecordKind.WinPlaceBracketOdds
        | "O2" -> RecordKind.QuinellaOdds
        | "O3" -> RecordKind.WideOdds
        | "O4" -> RecordKind.ExactaOdds
        | "O5" -> RecordKind.TrioOdds
        | "O6" -> RecordKind.TrifectaOdds
        | "UM" -> RecordKind.Horse
        | "KS" -> RecordKind.Jockey
        | "CH" -> RecordKind.Trainer
        | "BR" -> RecordKind.Breeder
        | "BN" -> RecordKind.Owner
        | "HN" -> RecordKind.BreedingHorse
        | "SK" -> RecordKind.Offspring
        | "CK" -> RecordKind.RaceFrequency
        | "RC" -> RecordKind.RaceRecord
        | "HC" -> RecordKind.HillTraining
        | "HS" -> RecordKind.MarketPrice
        | "HY" -> RecordKind.HorseNameOrigin
        | "YS" -> RecordKind.Schedule
        | "BT" -> RecordKind.Lineage
        | "CS" -> RecordKind.Course
        | "DM" -> RecordKind.TimeMining
        | "TM" -> RecordKind.MatchMining
        | "WF" -> RecordKind.Win5
        | "JG" -> RecordKind.Exclusion
        | "WC" -> RecordKind.WoodchipTraining
        | "WH" -> RecordKind.HorseWeight
        | "WE" -> RecordKind.Weather
        | "AV" -> RecordKind.Withdrawal
        | "JC" -> RecordKind.JockeyChange
        | "TC" -> RecordKind.TimeChange
        | "CC" -> RecordKind.CourseChange
        | id -> RecordKind.Unknown id

    let ofHeader (header: RecordHeader) = ofId header.RecordId
