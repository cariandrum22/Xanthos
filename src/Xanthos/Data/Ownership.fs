namespace Xanthos.Data

open Xanthos

/// FinishCounts contains first through fifth place, then all lower placings.
type OwnershipPerformance =
    { Year: Sourced<int option>
      BasePrizeHundredYen: Sourced<decimal option>
      AddedPrizeHundredYen: Sourced<decimal option>
      FinishCounts: Sourced<int option> array }

type Breeder =
    {
        Header: RecordHeader
        Format: IdentifierFormat
        BreederId: string
        Name: string
        NameWithoutLegalForm: string
        KanaName: string
        EuropeanName: string
        Address: string
        /// Current year, then lifetime totals.
        Performances: OwnershipPerformance array
        Raw: byte[]
    }

type Owner =
    {
        Header: RecordHeader
        OwnerId: string
        Name: string
        NameWithoutLegalForm: string
        KanaName: string
        EuropeanName: string
        RacingColours: string
        /// Current year, then lifetime totals.
        Performances: OwnershipPerformance array
        Raw: byte[]
    }

module internal OwnershipParser =
    let private performances (r: Reader) start =
        Array.init 2 (fun i ->
            let p = start + i * 60
            let name = $"Performances[{i}]"

            { Year = r.Int (name + ".Year") p 4
              BasePrizeHundredYen = r.Number (name + ".BasePrizeHundredYen") (p + 4) 10 1M
              AddedPrizeHundredYen = r.Number (name + ".AddedPrizeHundredYen") (p + 14) 10 1M
              FinishCounts = Array.init 6 (fun j -> r.Int ($"{name}.FinishCounts[{j}]") (p + 24 + j * 6) 6) })

    let parseBRWithFormat format data =
        let widths =
            match format with
            | IdentifierFormat.Expanded -> []
            | IdentifierFormat.Legacy -> [ 12, 8, 6; 20, 72, 70; 92, 72, 70; 164, 72, 70 ]

        Reader.parseWithWidths
            widths
            "BR"
            545
            [ "0"; "1"; "2" ]
            (fun r header ->
                { Header = header
                  Format = format
                  BreederId = r.Digits "BreederId" 12 8
                  Name = r.Text "Name" 20 72
                  NameWithoutLegalForm = r.Text "NameWithoutLegalForm" 92 72
                  KanaName = r.Text "KanaName" 164 72
                  EuropeanName = r.Text "EuropeanName" 236 168
                  Address = r.Text "Address" 404 20
                  Performances = performances r 424
                  Raw = Array.copy data }
                : Breeder)
            data

    let parseBR data =
        parseBRWithFormat IdentifierFormat.Expanded data

    let parseBN data =
        Reader.parse
            "BN"
            477
            [ "0"; "1"; "2" ]
            (fun r header ->
                { Header = header
                  OwnerId = r.Digits "OwnerId" 12 6
                  Name = r.Text "Name" 18 64
                  NameWithoutLegalForm = r.Text "NameWithoutLegalForm" 82 64
                  KanaName = r.Text "KanaName" 146 50
                  EuropeanName = r.Text "EuropeanName" 196 100
                  RacingColours = r.Text "RacingColours" 296 60
                  Performances = performances r 356
                  Raw = Array.copy data }
                : Owner)
            data
