namespace Xanthos.Data

open Xanthos

type HorseWeightEntry =
    { HorseNumber: Sourced<int option>
      Name: string
      Weight: Sourced<BodyWeight>
      ChangeSign: string
      Change: Sourced<WeightChange> }

type HorseWeight =
    { Header: RecordHeader
      Identity: RaceIdentity
      Announcement: Sourced<AnnouncementTime option>
      Horses: HorseWeightEntry array
      Raw: byte[] }

module internal HorseWeightParser =
    let parse data =
        Reader.parse
            "WH"
            847
            [ "1" ]
            (fun r header ->
                { Header = header
                  Identity = r.Identity 12
                  Announcement = r.Announcement "Announcement" 28
                  Horses =
                    Array.init 18 (fun i ->
                        let p = 36 + i * 45
                        let name = $"Horses[{i}]"
                        let weight = r.Int (name + ".Weight") (p + 38) 3
                        let sign = r.Ascii (name + ".ChangeSign") (p + 41) 1

                        if sign <> "+" && sign <> "-" && sign <> " " then
                            r.Fail (name + ".ChangeSign") (p + 41) 1 "Unknown weight-change sign."

                        let change = r.Int (name + ".Change") (p + 42) 3

                        { HorseNumber = r.Int (name + ".HorseNumber") p 2
                          Name = r.Text (name + ".Name") (p + 2) 36
                          Weight =
                            { Raw = weight.Raw
                              Value =
                                match weight.Value with
                                | None -> BodyWeight.Missing
                                | Some 0 -> BodyWeight.Withdrawn
                                | Some 999 -> BodyWeight.Unmeasurable
                                | Some n when n >= 2 && n <= 998 -> BodyWeight.Kilograms n
                                | _ -> r.Fail (name + ".Weight") (p + 38) 3 "Expected 002–998, 000, 999 or blanks." }
                          ChangeSign = sign
                          Change =
                            { Raw = change.Raw
                              Value =
                                match change.Value with
                                | None -> WeightChange.Missing
                                | Some 999 -> WeightChange.Unmeasurable
                                | Some n -> WeightChange.Kilograms(if sign = "-" then -n else n) } })
                  Raw = Array.copy data })
            data
