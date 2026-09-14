namespace Xanthos

/// Official record projection. Raw bytes remain available on every successful result.
[<RequireQualifiedAccess>]
module Records =
    /// Defaults decode the currently documented expanded identifiers and odds limits.
    type ParseOptions =
        { IdentifierFormat: Data.IdentifierFormat
          OddsLimitFormat: Data.OddsLimitFormat }

        static member Default =
            { IdentifierFormat = Data.IdentifierFormat.Expanded
              OddsLimitFormat = Data.OddsLimitFormat.Current }

    type TK = Data.SpecialRegistration
    type RA = Data.Race
    type SE = Data.Runner
    type HR = Data.Payoff
    type H1 = Data.Votes
    type H6 = Data.TrifectaVotes
    type O1 = Data.WinPlaceBracketOdds
    type O2 = Data.SingleOdds
    type O3 = Data.WideOdds
    type O4 = Data.SingleOdds
    type O5 = Data.SingleOdds
    type O6 = Data.SingleOdds
    type WE = Data.WeatherChange
    type AV = Data.Withdrawal
    type JC = Data.JockeyChange
    type TC = Data.TimeChange
    type CC = Data.CourseChange
    type WF = Data.Win5
    type BR = Data.Breeder
    type BN = Data.Owner
    type UM = Data.Horse
    type RC = Data.RaceRecord
    type KS = Data.Jockey
    type CH = Data.Trainer
    type HN = Data.BreedingHorse
    type SK = Data.Offspring
    type HS = Data.MarketPrice
    type HY = Data.HorseNameOrigin
    type JG = Data.Exclusion
    type HC = Data.HillTraining
    type WC = Data.WoodchipTraining
    type WH = Data.HorseWeight
    type YS = Data.Schedule
    type BT = Data.Lineage
    type CS = Data.Course
    type DM = Data.TimeMining
    type TM = Data.MatchMining
    type CK = Data.RaceFrequency


    [<RequireQualifiedAccess>]
    type Record =
        | TK of TK
        | RA of RA
        | SE of SE
        | HR of HR
        | H1 of H1
        | H6 of H6
        | O1 of O1
        | O2 of O2
        | O3 of O3
        | O4 of O4
        | O5 of O5
        | O6 of O6
        | WE of WE
        | AV of AV
        | JC of JC
        | TC of TC
        | CC of CC
        | WF of WF
        | BR of BR
        | BN of BN
        | UM of UM
        | RC of RC
        | KS of KS
        | CH of CH
        | HN of HN
        | SK of SK
        | HS of HS
        | HY of HY
        | JG of JG
        | HC of HC
        | WC of WC
        | WH of WH
        | YS of YS
        | BT of BT
        | CS of CS
        | DM of DM
        | TM of TM
        | CK of CK
        | Unknown of recordId: string * raw: byte[]

    let parseTK data =
        Data.SpecialRegistrationParser.parse data

    let parseRA data = Data.RaceParser.parse data
    let parseSE data = Data.RunnerParser.parse data
    let parseHR data = Data.PayoffParser.parse data
    let parseH1 data = Data.VotesParser.parseH1 data
    let parseH6 data = Data.VotesParser.parseH6 data
    let parseWE data = Data.ChangesParser.parseWE data
    let parseAV data = Data.ChangesParser.parseAV data
    let parseJC data = Data.ChangesParser.parseJC data
    let parseTC data = Data.ChangesParser.parseTC data
    let parseCC data = Data.ChangesParser.parseCC data
    let parseWF data = Data.Win5Parser.parse data
    let parseBR data = Data.OwnershipParser.parseBR data

    let parseBRWithFormat format data =
        Data.OwnershipParser.parseBRWithFormat format data

    let parseBN data = Data.OwnershipParser.parseBN data
    let parseUM data = Data.HorseParser.parse data

    let parseUMWithFormat format data =
        Data.HorseParser.parseWithFormat format data

    let parseRC data = Data.RaceRecordParser.parse data
    let parseKS data = Data.PeopleParser.parseKS data
    let parseCH data = Data.PeopleParser.parseCH data
    let parseO1WithFormat format data = Data.OddsParser.parseO1 format data
    let parseO2WithFormat format data = Data.OddsParser.parseO2 format data
    let parseO3WithFormat format data = Data.OddsParser.parseO3 format data
    let parseO4WithFormat format data = Data.OddsParser.parseO4 format data
    let parseO5WithFormat format data = Data.OddsParser.parseO5 format data
    let parseO6WithFormat format data = Data.OddsParser.parseO6 format data

    let parseO1 data =
        parseO1WithFormat Data.OddsLimitFormat.Current data

    let parseO2 data =
        parseO2WithFormat Data.OddsLimitFormat.Current data

    let parseO3 data =
        parseO3WithFormat Data.OddsLimitFormat.Current data

    let parseO4 data =
        parseO4WithFormat Data.OddsLimitFormat.Current data

    let parseO5 data =
        parseO5WithFormat Data.OddsLimitFormat.Current data

    let parseO6 data =
        parseO6WithFormat Data.OddsLimitFormat.Current data

    let parseHNWithFormat format data = Data.BreedingParser.parseHN format data

    let parseHN data =
        parseHNWithFormat Data.IdentifierFormat.Expanded data

    let parseSKWithFormat format data = Data.BreedingParser.parseSK format data

    let parseSK data =
        parseSKWithFormat Data.IdentifierFormat.Expanded data

    let parseHSWithFormat format data = Data.BreedingParser.parseHS format data

    let parseHS data =
        parseHSWithFormat Data.IdentifierFormat.Expanded data

    let parseHY data = Data.BreedingParser.parseHY data
    let parseJG data = Data.ExclusionParser.parse data
    let parseHC data = Data.TrainingParser.parseHC data
    let parseWC data = Data.TrainingParser.parseWC data
    let parseWH data = Data.HorseWeightParser.parse data
    let parseYS data = Data.ReferenceDataParser.parseYS data

    let parseBTWithFormat format data =
        Data.ReferenceDataParser.parseBT format data

    let parseBT data =
        parseBTWithFormat Data.IdentifierFormat.Expanded data

    let parseCS data = Data.ReferenceDataParser.parseCS data
    let parseDM data = Data.MiningParser.parseDM data
    let parseTM data = Data.MiningParser.parseTM data

    let parseCKWithFormat format data =
        Data.RaceFrequencyParser.parse format data

    let parseCK data =
        parseCKWithFormat Data.IdentifierFormat.Expanded data

    let parseWith options (data: byte[]) =
        RecordBytes.ascii "" "RecordId" 1 2 data
        |> Result.bind (fun id ->
            match RecordKinds.ofId id with
            | RecordKind.SpecialRegistration -> parseTK data |> Result.map Record.TK
            | RecordKind.Race -> parseRA data |> Result.map Record.RA
            | RecordKind.Runner -> parseSE data |> Result.map Record.SE
            | RecordKind.Payoff -> parseHR data |> Result.map Record.HR
            | RecordKind.Votes -> parseH1 data |> Result.map Record.H1
            | RecordKind.TrifectaVotes -> parseH6 data |> Result.map Record.H6
            | RecordKind.WinPlaceBracketOdds -> parseO1WithFormat options.OddsLimitFormat data |> Result.map Record.O1
            | RecordKind.QuinellaOdds -> parseO2WithFormat options.OddsLimitFormat data |> Result.map Record.O2
            | RecordKind.WideOdds -> parseO3WithFormat options.OddsLimitFormat data |> Result.map Record.O3
            | RecordKind.ExactaOdds -> parseO4WithFormat options.OddsLimitFormat data |> Result.map Record.O4
            | RecordKind.TrioOdds -> parseO5WithFormat options.OddsLimitFormat data |> Result.map Record.O5
            | RecordKind.TrifectaOdds -> parseO6WithFormat options.OddsLimitFormat data |> Result.map Record.O6
            | RecordKind.Weather -> parseWE data |> Result.map Record.WE
            | RecordKind.Withdrawal -> parseAV data |> Result.map Record.AV
            | RecordKind.JockeyChange -> parseJC data |> Result.map Record.JC
            | RecordKind.TimeChange -> parseTC data |> Result.map Record.TC
            | RecordKind.CourseChange -> parseCC data |> Result.map Record.CC
            | RecordKind.Win5 -> parseWF data |> Result.map Record.WF
            | RecordKind.Breeder -> parseBRWithFormat options.IdentifierFormat data |> Result.map Record.BR
            | RecordKind.Owner -> parseBN data |> Result.map Record.BN
            | RecordKind.Horse -> parseUMWithFormat options.IdentifierFormat data |> Result.map Record.UM
            | RecordKind.RaceRecord -> parseRC data |> Result.map Record.RC
            | RecordKind.Jockey -> parseKS data |> Result.map Record.KS
            | RecordKind.Trainer -> parseCH data |> Result.map Record.CH
            | RecordKind.BreedingHorse -> parseHNWithFormat options.IdentifierFormat data |> Result.map Record.HN
            | RecordKind.Offspring -> parseSKWithFormat options.IdentifierFormat data |> Result.map Record.SK
            | RecordKind.MarketPrice -> parseHSWithFormat options.IdentifierFormat data |> Result.map Record.HS
            | RecordKind.HorseNameOrigin -> parseHY data |> Result.map Record.HY
            | RecordKind.Exclusion -> parseJG data |> Result.map Record.JG
            | RecordKind.HillTraining -> parseHC data |> Result.map Record.HC
            | RecordKind.WoodchipTraining -> parseWC data |> Result.map Record.WC
            | RecordKind.HorseWeight -> parseWH data |> Result.map Record.WH
            | RecordKind.Schedule -> parseYS data |> Result.map Record.YS
            | RecordKind.Lineage -> parseBTWithFormat options.IdentifierFormat data |> Result.map Record.BT
            | RecordKind.Course -> parseCS data |> Result.map Record.CS
            | RecordKind.TimeMining -> parseDM data |> Result.map Record.DM
            | RecordKind.MatchMining -> parseTM data |> Result.map Record.TM
            | RecordKind.RaceFrequency -> parseCKWithFormat options.IdentifierFormat data |> Result.map Record.CK
            | RecordKind.Unknown _ -> Ok(Record.Unknown(id, Array.copy data)))

    let parse data = parseWith ParseOptions.Default data
