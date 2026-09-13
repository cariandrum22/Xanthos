namespace Xanthos.Data

open System
open Xanthos

/// Interpreted values retain their exact fixed-width source representation.
type Sourced<'value> = { Raw: string; Value: 'value }

/// Select from the requested dataspec, not the record creation date.
[<RequireQualifiedAccess>]
type IdentifierFormat =
    | Expanded
    | Legacy

type RaceIdentity =
    { Raw: string
      Year: Sourced<int option>
      MonthDay: string
      Date: DateOnly option
      Racecourse: OfficialCode
      Meeting: Sourced<int option>
      Day: Sourced<int option>
      RaceNumber: Sourced<int option> }

type RaceName =
    { Weekday: OfficialCode
      SpecialRaceNumber: Sourced<int option>
      Title: string
      Subtitle: string
      Parentheses: string
      EnglishTitle: string
      EnglishSubtitle: string
      EnglishParentheses: string
      Abbreviation10: string
      Abbreviation6: string
      Abbreviation3: string
      Category: string
      Edition: Sourced<int option> }

type RaceConditions =
    { RaceKind: OfficialCode
      RaceSymbol: OfficialCode
      WeightRule: OfficialCode
      AgeConditions: OfficialCode array }

type RegisteredHorse =
    { Sequence: Sourced<int option>
      PedigreeId: string
      Name: string
      HorseSymbol: OfficialCode
      Sex: OfficialCode
      TrainerAffiliation: OfficialCode
      TrainerCode: string
      TrainerName: string
      AssignedWeight: Sourced<decimal option>
      ExchangeCategory: string }

type SpecialRegistration =
    { Header: RecordHeader
      Identity: RaceIdentity
      Name: RaceName
      Grade: OfficialCode
      Conditions: RaceConditions
      Distance: Sourced<int option>
      Track: OfficialCode
      CourseCategory: string
      HandicapDate: Sourced<DateOnly option>
      RegisteredCount: Sourced<int option>
      Horses: RegisteredHorse array
      Raw: byte[] }

/// Month, day and time as supplied by the SDK; no year is present in this field.
type AnnouncementTime =
    { Month: int
      Day: int
      Hour: int
      Minute: int }
