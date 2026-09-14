namespace Xanthos.Data

open System
open Xanthos

[<RequireQualifiedAccess>]
type ImportKind =
    | Domestic
    | ImportedInUtero
    | ImportedAsDomestic
    | Imported
    | Other
    | Unknown of string

type BreedingHorse =
    { Header: RecordHeader
      Format: IdentifierFormat
      BreedingId: string
      PedigreeId: string
      Name: string
      KanaName: string
      EuropeanName: string
      BirthYear: Sourced<int option>
      Sex: OfficialCode
      Breed: OfficialCode
      Coat: OfficialCode
      ImportKind: Sourced<ImportKind>
      ImportYear: Sourced<int option>
      Birthplace: string
      FatherBreedingId: string
      MotherBreedingId: string
      Raw: byte[] }

type Offspring =
    {
        Header: RecordHeader
        Format: IdentifierFormat
        PedigreeId: string
        BirthDate: Sourced<DateOnly option>
        Sex: OfficialCode
        Breed: OfficialCode
        Coat: OfficialCode
        ImportKind: Sourced<ImportKind>
        ImportYear: Sourced<int option>
        BreederId: string
        Birthplace: string
        /// Father, mother, then the paternal-to-maternal order for each generation.
        AncestorBreedingIds: string array
        Raw: byte[]
    }

type MarketPrice =
    { Header: RecordHeader
      Format: IdentifierFormat
      PedigreeId: string
      FatherBreedingId: string
      MotherBreedingId: string
      BirthYear: Sourced<int option>
      MarketId: string
      Organizer: string
      MarketName: string
      StartDate: Sourced<DateOnly option>
      EndDate: Sourced<DateOnly option>
      Age: Sourced<int option>
      PriceYen: Sourced<decimal option>
      Raw: byte[] }

type HorseNameOrigin =
    { Header: RecordHeader
      PedigreeId: string
      Name: string
      Origin: string
      Raw: byte[] }

module internal BreedingParser =
    let private origin allowOther (r: Reader) p =
        let raw = r.Ascii "ImportKind" p 1

        { Raw = raw
          Value =
            match raw with
            | "0" -> ImportKind.Domestic
            | "1" -> ImportKind.ImportedInUtero
            | "2" -> ImportKind.ImportedAsDomestic
            | "3" -> ImportKind.Imported
            | "9" when allowOther -> ImportKind.Other
            | raw -> ImportKind.Unknown raw }

    let parseHN format data =
        let widths =
            if format = IdentifierFormat.Legacy then
                [ 12, 10, 8; 230, 10, 8; 240, 10, 8 ]
            else
                []

        Reader.parseWithWidths
            widths
            "HN"
            251
            [ "0"; "1"; "2" ]
            (fun r header ->
                { Header = header
                  Format = format
                  BreedingId = r.Digits "BreedingId" 12 10
                  PedigreeId = r.Digits "PedigreeId" 30 10
                  Name = r.Text "Name" 41 36
                  KanaName = r.Text "KanaName" 77 40
                  EuropeanName = r.Text "EuropeanName" 117 80
                  BirthYear = r.Int "BirthYear" 197 4
                  Sex = r.Code CodeTable.Sex "Sex" 201
                  Breed = r.Code CodeTable.Breed "Breed" 202
                  Coat = r.Code CodeTable.Coat "Coat" 203
                  ImportKind = origin true r 205
                  ImportYear = r.Int "ImportYear" 206 4
                  Birthplace = r.Text "Birthplace" 210 20
                  FatherBreedingId = r.Digits "FatherBreedingId" 230 10
                  MotherBreedingId = r.Digits "MotherBreedingId" 240 10
                  Raw = Array.copy data }
                : BreedingHorse)
            data

    let parseSK format data =
        let widths =
            if format = IdentifierFormat.Legacy then
                [ yield 39, 8, 6
                  for i in 0..13 do
                      yield 67 + i * 10, 10, 8 ]
            else
                []

        Reader.parseWithWidths
            widths
            "SK"
            208
            [ "0"; "1"; "2" ]
            (fun r header ->
                { Header = header
                  Format = format
                  PedigreeId = r.Digits "PedigreeId" 12 10
                  BirthDate = r.Date "BirthDate" 22
                  Sex = r.Code CodeTable.Sex "Sex" 30
                  Breed = r.Code CodeTable.Breed "Breed" 31
                  Coat = r.Code CodeTable.Coat "Coat" 32
                  ImportKind = origin false r 34
                  ImportYear = r.Int "ImportYear" 35 4
                  BreederId = r.Digits "BreederId" 39 8
                  Birthplace = r.Text "Birthplace" 47 20
                  AncestorBreedingIds =
                    Array.init 14 (fun i -> r.Digits ($"AncestorBreedingIds[{i}]") (67 + i * 10) 10)
                  Raw = Array.copy data }
                : Offspring)
            data

    let parseHS format data =
        let widths =
            if format = IdentifierFormat.Legacy then
                [ 22, 10, 8; 32, 10, 8 ]
            else
                []

        Reader.parseWithWidths
            widths
            "HS"
            200
            [ "0"; "1" ]
            (fun r header ->
                { Header = header
                  Format = format
                  PedigreeId = r.Digits "PedigreeId" 12 10
                  FatherBreedingId = r.Digits "FatherBreedingId" 22 10
                  MotherBreedingId = r.Digits "MotherBreedingId" 32 10
                  BirthYear = r.Int "BirthYear" 42 4
                  MarketId = r.Digits "MarketId" 46 6
                  Organizer = r.Text "Organizer" 52 40
                  MarketName = r.Text "MarketName" 92 80
                  StartDate = r.Date "StartDate" 172
                  EndDate = r.Date "EndDate" 180
                  Age = r.Int "Age" 188 1
                  PriceYen = r.Number "PriceYen" 189 10 1M
                  Raw = Array.copy data }
                : MarketPrice)
            data

    let parseHY data =
        Reader.parse
            "HY"
            123
            [ "0"; "1" ]
            (fun r header ->
                { Header = header
                  PedigreeId = r.Digits "PedigreeId" 12 10
                  Name = r.Text "Name" 22 36
                  Origin = r.Text "Origin" 58 64
                  Raw = Array.copy data })
            data
