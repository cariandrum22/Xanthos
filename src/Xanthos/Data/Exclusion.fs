namespace Xanthos.Data

open Xanthos

[<RequireQualifiedAccess>]
type EntryStatus =
    | Entered
    | ExcludedAtDeadline
    | Reentered
    | ReentryExcluded
    | WithdrawnWithoutNumber
    | Withdrawn
    | Unknown of string

[<RequireQualifiedAccess>]
type ExclusionStatus =
    | NotBalloted
    | NotSelected
    | Unknown of string

type Exclusion =
    { Header: RecordHeader
      Identity: RaceIdentity
      PedigreeId: string
      Name: string
      EntrySequence: Sourced<int option>
      EntryStatus: Sourced<EntryStatus>
      ExclusionStatus: Sourced<ExclusionStatus>
      Raw: byte[] }

module internal ExclusionParser =
    let parse data =
        Reader.parse
            "JG"
            80
            [ "0"; "1" ]
            (fun r header ->
                let entry = r.Ascii "EntryStatus" 77 1
                let excluded = r.Ascii "ExclusionStatus" 78 1

                { Header = header
                  Identity = r.Identity 12
                  PedigreeId = r.Digits "PedigreeId" 28 10
                  Name = r.Text "Name" 38 36
                  EntrySequence = r.Int "EntrySequence" 74 3
                  EntryStatus =
                    { Raw = entry
                      Value =
                        match entry with
                        | "1" -> EntryStatus.Entered
                        | "2" -> EntryStatus.ExcludedAtDeadline
                        | "4" -> EntryStatus.Reentered
                        | "5" -> EntryStatus.ReentryExcluded
                        | "6" -> EntryStatus.WithdrawnWithoutNumber
                        | "9" -> EntryStatus.Withdrawn
                        | raw -> EntryStatus.Unknown raw }
                  ExclusionStatus =
                    { Raw = excluded
                      Value =
                        match excluded with
                        | "1" -> ExclusionStatus.NotBalloted
                        | "2" -> ExclusionStatus.NotSelected
                        | raw -> ExclusionStatus.Unknown raw }
                  Raw = Array.copy data })
            data
