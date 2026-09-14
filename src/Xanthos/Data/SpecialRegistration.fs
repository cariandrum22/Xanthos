namespace Xanthos.Data

open Xanthos

module internal SpecialRegistrationParser =
    let parse (data: byte[]) =
        Reader.parse
            "TK"
            21657
            [ "0"; "1"; "2" ]
            (fun reader header ->
                let count = reader.Int "RegisteredCount" 653 3

                if count.Value |> Option.exists (fun n -> n > 300) then
                    reader.Fail "RegisteredCount" 653 3 "The record contains at most 300 registration slots."

                { Header = header
                  Identity = reader.Identity 12
                  Name = reader.RaceName 28
                  Grade = reader.Code CodeTable.Grade "Grade" 615
                  Conditions = reader.Conditions 616
                  Distance = reader.Int "Distance" 637 4
                  Track = reader.Code CodeTable.Track "Track" 641
                  CourseCategory = reader.Ascii "CourseCategory" 643 2
                  HandicapDate = reader.Date "HandicapDate" 645
                  RegisteredCount = count
                  Horses =
                    Array.init 300 (fun i ->
                        let offset = 655 + i * 70
                        let field name = $"Horses[{i}].{name}"
                        let unused = i >= (count.Value |> Option.defaultValue 0)

                        { Sequence = reader.Int (field "Sequence") (offset + 1) 3
                          PedigreeId = reader.Identifier unused (field "PedigreeId") (offset + 4) 10
                          Name = reader.Text (field "Name") (offset + 14) 36
                          HorseSymbol = reader.Code CodeTable.HorseSymbol (field "HorseSymbol") (offset + 50)
                          Sex = reader.Code CodeTable.Sex (field "Sex") (offset + 52)
                          TrainerAffiliation =
                            reader.Code CodeTable.Affiliation (field "TrainerAffiliation") (offset + 53)
                          TrainerCode = reader.Identifier unused (field "TrainerCode") (offset + 54) 5
                          TrainerName = reader.Text (field "TrainerName") (offset + 59) 8
                          AssignedWeight = reader.Number (field "AssignedWeight") (offset + 67) 3 10M
                          ExchangeCategory = reader.Ascii (field "ExchangeCategory") (offset + 70) 1 })
                  Raw = Array.copy data })
            data
