/// <summary>
/// Core domain types for JV-Link data representation.
/// These types model the fundamental entities in JRA (Japan Racing Association) horse racing data.
/// </summary>
/// <remarks>
/// JV-Link provides racing data in Shift-JIS encoded fixed-length records.
/// These domain types provide a type-safe F# representation of that data.
/// For field mappings and record formats, see the JV-Data specification in docs/official/.
/// </remarks>
namespace Xanthos.Core

open System
open System.Globalization
open Xanthos.Core.Errors

/// <summary>
/// A unique identifier for a race in the JV-Link system.
/// </summary>
/// <remarks>
/// RaceId typically corresponds to the JV-Data key format: YYYYMMDDJJRRNN
/// where YYYYMMDD is the date, JJ is the venue code, RR is the race number, etc.
/// Use <see cref="RaceId.create"/> to construct with validation or <see cref="RaceId.unsafe"/> for trusted input.
/// </remarks>
[<Struct>]
type RaceId = private RaceId of string

/// <summary>Functions for creating and working with RaceId values.</summary>
module RaceId =
    /// <summary>Minimum length for a valid RaceId (YYYYMMDD).</summary>
    let private minLength = 8

    /// <summary>Validates that the first 8 characters form a valid date.</summary>
    let private hasValidDatePrefix (s: string) =
        if s.Length < 8 then
            false
        else
            let datePart = s.Substring(0, 8)
            // All date characters must be digits
            if not (datePart |> Seq.forall Char.IsDigit) then
                false
            else
                let mutable parsed = DateTime.MinValue

                DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, &parsed)

    /// <summary>Validates that all characters after the date prefix are alphanumeric.</summary>
    let private hasValidSuffix (s: string) =
        if s.Length <= 8 then
            true
        else
            s.Substring(8) |> Seq.forall Char.IsLetterOrDigit

    /// <summary>
    /// Creates a RaceId with validation.
    /// </summary>
    /// <param name="value">The race identifier string.</param>
    /// <returns>Ok(RaceId) if valid, Error with validation message if invalid.</returns>
    /// <remarks>
    /// Validation rules:
    /// - Must not be empty or whitespace
    /// - Must be at least 8 characters (YYYYMMDD minimum)
    /// - First 8 characters must form a valid date in yyyyMMdd format
    /// - Remaining characters (if any) must be alphanumeric
    /// </remarks>
    let create value =
        if String.IsNullOrWhiteSpace value then
            Error(validation "RaceId cannot be empty.")
        else
            let trimmed = value.Trim()

            if trimmed.Length < minLength then
                Error(validation $"RaceId must be at least {minLength} characters (YYYYMMDD minimum).")
            elif not (hasValidDatePrefix trimmed) then
                Error(validation "RaceId must start with a valid date in yyyyMMdd format.")
            elif not (hasValidSuffix trimmed) then
                Error(validation "RaceId suffix must contain only alphanumeric characters.")
            else
                Ok(RaceId trimmed)

    /// <summary>Extracts the underlying string value from a RaceId.</summary>
    /// <param name="RaceId v">The RaceId to unwrap.</param>
    /// <returns>The raw string identifier.</returns>
    let value (RaceId v) = v

    /// <summary>
    /// Creates a RaceId without validation. Use only for trusted input.
    /// </summary>
    /// <param name="value">The race identifier string (must be valid).</param>
    /// <returns>A RaceId wrapping the given value.</returns>
    let unsafe value = RaceId value

/// <summary>
/// A unique identifier for a runner (horse) in the JV-Link system.
/// </summary>
/// <remarks>
/// RunnerId corresponds to the horse's registration number (血統登録番号) in JRA records.
/// This is a stable identifier that persists across races and seasons.
/// </remarks>
[<Struct>]
type RunnerId = private RunnerId of string

/// <summary>Functions for creating and working with RunnerId values.</summary>
module RunnerId =
    /// <summary>Expected length for blood registration number (血統登録番号).</summary>
    let private expectedLength = 10

    /// <summary>
    /// Creates a RunnerId with validation.
    /// </summary>
    /// <param name="value">The runner identifier string.</param>
    /// <returns>Ok(RunnerId) if valid, Error with validation message if invalid.</returns>
    /// <remarks>
    /// Validation rules:
    /// - Must not be empty or whitespace
    /// - Must contain only digits (blood registration number is numeric)
    /// - Must be exactly 10 characters (standard blood registration number length)
    /// </remarks>
    let create value =
        if String.IsNullOrWhiteSpace value then
            Error(validation "RunnerId cannot be empty.")
        else
            let trimmed = value.Trim()

            if not (trimmed |> Seq.forall Char.IsDigit) then
                Error(validation "RunnerId must contain only digits.")
            elif trimmed.Length <> expectedLength then
                Error(
                    validation
                        $"RunnerId must be exactly {expectedLength} characters (blood registration number format)."
                )
            else
                Ok(RunnerId trimmed)

    /// <summary>Extracts the underlying string value from a RunnerId.</summary>
    /// <param name="RunnerId v">The RunnerId to unwrap.</param>
    /// <returns>The raw string identifier.</returns>
    let value (RunnerId v) = v

    /// <summary>
    /// Creates a RunnerId without validation. Use only for trusted input.
    /// </summary>
    /// <param name="value">The runner identifier string (must be valid).</param>
    /// <returns>A RunnerId wrapping the given value.</returns>
    let unsafe value = RunnerId value

/// <summary>
/// Represents the surface type of a race track.
/// </summary>
/// <remarks>
/// JV-Data uses numeric codes for track surface: 1=芝(Turf), 2=ダート(Dirt), 3=障害芝, 4=障害ダート.
/// See JV-Data specification section 2.1 (コード表) for complete mappings.
/// </remarks>
type TrackSurface =
    /// <summary>Turf (芝) - grass surface.</summary>
    | Turf
    /// <summary>Dirt (ダート) - sand/dirt surface.</summary>
    | Dirt
    /// <summary>Synthetic surface (all-weather track).</summary>
    | Synthetic
    /// <summary>Unknown or unrecognized surface code.</summary>
    | UnknownSurface

/// <summary>
/// Represents the condition of the track surface.
/// </summary>
/// <remarks>
/// JV-Data馬場状態コード: 1=良(Fast), 2=稍重(Good), 3=重(Yielding), 4=不良(Heavy).
/// Mapping varies between JRA official terminology and international standards.
/// </remarks>
type TrackCondition =
    /// <summary>Fast/Firm (良) - optimal dry conditions.</summary>
    | Fast
    /// <summary>Good (稍重) - slightly soft.</summary>
    | Good
    /// <summary>Yielding (重) - soft ground.</summary>
    | Yielding
    /// <summary>Soft - very soft ground (rarely used in JRA).</summary>
    | Soft
    /// <summary>Heavy (不良) - waterlogged/very soft.</summary>
    | Heavy
    /// <summary>Unknown or unrecognized condition code.</summary>
    | UnknownCondition

/// <summary>
/// Odds information for a single runner in a race.
/// </summary>
/// <remarks>
/// Odds are expressed as decimal multipliers (e.g., 2.5 means 2.5x return on a winning bet).
/// JV-Data provides odds in 1/10 units, which are converted to decimal form.
/// </remarks>
type RunnerOdds =
    {
        /// <summary>The runner's unique identifier.</summary>
        Runner: RunnerId
        /// <summary>Win odds (単勝) as a decimal multiplier, if available.</summary>
        WinOdds: decimal option
        /// <summary>Place odds (複勝) as a decimal multiplier, if available.</summary>
        PlaceOdds: decimal option
    }

/// <summary>
/// Basic information about a race.
/// </summary>
/// <remarks>
/// Corresponds to race header data from JV-Data RA (レース詳細) records.
/// Distance is in meters; Japanese races typically range from 1000m to 3600m.
/// </remarks>
type RaceInfo =
    {
        /// <summary>Unique identifier for this race.</summary>
        Id: RaceId
        /// <summary>Race name (e.g., "有馬記念", "日本ダービー").</summary>
        Name: string
        /// <summary>Course/venue name (e.g., "東京", "中山"), if available.</summary>
        Course: string option
        /// <summary>Race distance in meters, if available.</summary>
        DistanceMeters: int option
        /// <summary>Track surface type.</summary>
        Surface: TrackSurface
        /// <summary>Current track condition.</summary>
        Condition: TrackCondition
        /// <summary>Scheduled post time, if available.</summary>
        ScheduledStart: DateTime option
    }

/// <summary>
/// Odds information for all runners in a race at a specific point in time.
/// </summary>
/// <remarks>
/// Odds are updated periodically during betting windows.
/// The Timestamp indicates when this snapshot was captured.
/// </remarks>
type RaceOdds =
    {
        /// <summary>The race these odds apply to.</summary>
        Race: RaceId
        /// <summary>When these odds were captured.</summary>
        Timestamp: DateTime
        /// <summary>Odds for each runner in the race.</summary>
        Entries: RunnerOdds list
    }

/// <summary>
/// A discriminated union representing parsed JV-Link record types.
/// </summary>
/// <remarks>
/// JV-Link returns data as fixed-length Shift-JIS encoded records.
/// This type provides a type-safe wrapper for the parsed content.
/// Raw is used for record types not yet implemented with full parsing.
/// </remarks>
type JvRecord =
    /// <summary>A parsed race card containing race information.</summary>
    | RaceCard of RaceInfo
    /// <summary>Parsed odds data for a race.</summary>
    | Odds of RaceOdds
    /// <summary>Unparsed raw bytes for unsupported record types.</summary>
    | Raw of byte[]

/// <summary>
/// Represents the different event notifications emitted by JVWatchEvent.
/// </summary>
/// <remarks>
/// JVWatchEvent is a real-time notification API that pushes updates during live race days.
/// Each event type corresponds to a specific dataspec for retrieving the associated data.
/// See <see cref="WatchEvent.dataspecForEvent"/> for the dataspec mappings.
/// </remarks>
type WatchEventType =
    /// <summary>Payoff/dividend results confirmed (払戻確定). Dataspec: 0B12.</summary>
    | PayoffConfirmed
    /// <summary>Horse weight announced (馬体重発表). Dataspec: 0B11.</summary>
    | HorseWeight
    /// <summary>Jockey change (騎手変更). Dataspec: 0B16.</summary>
    | JockeyChange
    /// <summary>Weather condition change (天候変更). Dataspec: 0B16.</summary>
    | WeatherChange
    /// <summary>Course/track change (コース変更). Dataspec: 0B16.</summary>
    | CourseChange
    /// <summary>Race avoided/cancelled (発走除外). Dataspec: 0B16.</summary>
    | AvoidedRace
    /// <summary>Post time change (発走時刻変更). Dataspec: 0B16.</summary>
    | StartTimeChange
    /// <summary>Unknown or unrecognized event type with the raw event string.</summary>
    | UnknownEvent of string

/// <summary>
/// Payload of a JVWatchEvent callback containing event details.
/// </summary>
/// <remarks>
/// This record contains all parsed information from a watch event notification.
/// Use <see cref="WatchEvent.toRealtimeRequest"/> to convert to a JVRTOpen request.
/// </remarks>
type WatchEvent =
    {
        /// <summary>The type of event that occurred.</summary>
        Event: WatchEventType
        /// <summary>Raw key string from JVWatchEvent, used for JVRTOpen calls.</summary>
        RawKey: string
        /// <summary>Timestamp of the event, if parseable from the key.</summary>
        Timestamp: DateTime option
        /// <summary>Meeting date (開催日), if available.</summary>
        MeetingDate: DateTime option
        /// <summary>Course/venue code (競馬場コード), if available.</summary>
        CourseCode: string option
        /// <summary>Race number within the meeting, if available.</summary>
        RaceNumber: string option
        /// <summary>Record type identifier, if available.</summary>
        RecordType: string option
        /// <summary>Participant ID (horse/jockey), if relevant to the event.</summary>
        ParticipantId: string option
        /// <summary>Additional event-specific data, if any.</summary>
        AdditionalData: string option
    }

/// <summary>
/// Represents the dataspec/key pair required to issue JVRTOpen after a watch event.
/// </summary>
/// <remarks>
/// After receiving a watch event, use this request to fetch the associated real-time data.
/// The Dataspec identifies the data type, and Key specifies the exact record.
/// </remarks>
type WatchEventRealtimeRequest =
    {
        /// <summary>JV-Data specification code (e.g., "0B11", "0B12", "0B16").</summary>
        Dataspec: string
        /// <summary>Record key for the JVRTOpen call.</summary>
        Key: string
    }

/// <summary>Functions for working with WatchEvent values.</summary>
module WatchEvent =
    /// <summary>
    /// Maps a WatchEventType to its corresponding JV-Data specification code.
    /// </summary>
    /// <param name="eventType">The event type to map.</param>
    /// <returns>Some dataspec code if known, None for unknown events.</returns>
    let dataspecForEvent =
        function
        | WatchEventType.PayoffConfirmed -> Some "0B12"
        | WatchEventType.HorseWeight -> Some "0B11"
        | WatchEventType.JockeyChange
        | WatchEventType.WeatherChange
        | WatchEventType.CourseChange
        | WatchEventType.AvoidedRace
        | WatchEventType.StartTimeChange -> Some "0B16"
        | WatchEventType.UnknownEvent _ -> None

    /// <summary>
    /// Attempts to extract a DateTime from common watch event keys.
    /// </summary>
    /// <param name="rawKey">The raw key string (expected format: yyyyMMdd...).</param>
    /// <returns>Some DateTime if the first 8 characters parse as a date, None otherwise.</returns>
    let tryExtractFromTime (rawKey: string) : DateTime option =
        if String.IsNullOrWhiteSpace rawKey || rawKey.Length < 8 then
            None
        else
            let datePart = rawKey.Substring(0, 8)

            match DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | true, dt -> Some dt
            | _ -> None

    /// <summary>
    /// Converts a WatchEvent to a WatchEventRealtimeRequest for JVRTOpen.
    /// </summary>
    /// <param name="event">The watch event to convert.</param>
    /// <returns>Some request if the event type has a known dataspec, None otherwise.</returns>
    let toRealtimeRequest (event: WatchEvent) : WatchEventRealtimeRequest option =
        dataspecForEvent event.Event
        |> Option.map (fun dataspec ->
            { Dataspec = dataspec
              Key = event.RawKey })

/// <summary>
/// Represents a course diagram fetched from JV-Link via JVCourseFile.
/// </summary>
/// <remarks>
/// Course diagrams are image files showing the track layout.
/// JV-Link saves them to the configured save path.
/// </remarks>
type CourseDiagram =
    {
        /// <summary>Path to the saved diagram file.</summary>
        FilePath: string
        /// <summary>Optional textual explanation of the course.</summary>
        Explanation: string option
    }

/// <summary>
/// Represents a retrieved silks (勝負服) image via JVFukuFile.
/// </summary>
/// <remarks>
/// Silks images show the jockey's racing colors for visual identification.
/// Either FilePath (saved to disk) or Data (in-memory) will be populated.
/// </remarks>
type SilksImage =
    {
        /// <summary>Path to the saved silks image file, if saved to disk.</summary>
        FilePath: string option
        /// <summary>Raw image data as bytes, if loaded in memory.</summary>
        Data: byte[] option
    }

/// <summary>
/// Represents JV-Link movie categories for JRA-VAN Racing Viewer integration.
/// </summary>
/// <remarks>
/// Movie types map to two-digit codes used by JVMVCheck and JVMVPlay.
/// Race videos (00-03) and workout videos (11-13) use different code ranges.
/// </remarks>
type MovieType =
    /// <summary>Race video (レース映像). Code: 00.</summary>
    | RaceVideo
    /// <summary>Paddock video (パドック映像). Code: 01.</summary>
    | PaddockVideo
    /// <summary>Multi-camera race video (マルチカメラ映像). Code: 02.</summary>
    | MultiCameraVideo
    /// <summary>Patrol video (パトロール映像). Code: 03.</summary>
    | PatrolVideo
    /// <summary>All workout videos for a week (週間調教動画一覧). Code: 11.</summary>
    | WorkoutWeekAll
    /// <summary>Workout videos for a specific horse in a week (週間馬別調教動画). Code: 12.</summary>
    | WorkoutWeekHorse
    /// <summary>All workout videos for a horse (馬別調教動画一覧). Code: 13.</summary>
    | WorkoutHorseAll
    /// <summary>Custom movie type with raw code for future extensions.</summary>
    | CustomMovieType of string

/// <summary>Functions for working with MovieType values.</summary>
module MovieType =
    /// <summary>
    /// Converts a MovieType to its two-digit JV-Link code.
    /// </summary>
    /// <param name="movieType">The movie type to convert.</param>
    /// <returns>The two-digit code string.</returns>
    let toCode =
        function
        | RaceVideo -> "00"
        | PaddockVideo -> "01"
        | MultiCameraVideo -> "02"
        | PatrolVideo -> "03"
        | WorkoutWeekAll -> "11"
        | WorkoutWeekHorse -> "12"
        | WorkoutHorseAll -> "13"
        | CustomMovieType code -> code

    /// <summary>
    /// Creates a MovieType from a two-digit JV-Link code.
    /// </summary>
    /// <param name="code">The two-digit code string.</param>
    /// <returns>The corresponding MovieType, or CustomMovieType for unrecognized codes.</returns>
    let fromCode code =
        match code with
        | "00" -> RaceVideo
        | "01" -> PaddockVideo
        | "02" -> MultiCameraVideo
        | "03" -> PatrolVideo
        | "11" -> WorkoutWeekAll
        | "12" -> WorkoutWeekHorse
        | "13" -> WorkoutHorseAll
        | other -> CustomMovieType other

    /// <summary>
    /// Determines if a MovieType is a workout (調教) video type.
    /// </summary>
    /// <param name="movieType">The movie type to check.</param>
    /// <returns>True if this is a workout video type (codes starting with "1").</returns>
    let isWorkoutType =
        function
        | WorkoutWeekAll
        | WorkoutWeekHorse
        | WorkoutHorseAll -> true
        | CustomMovieType code when code.StartsWith("1", StringComparison.Ordinal) -> true
        | _ -> false

/// <summary>
/// Represents a single entry returned by JVMVRead for workout video listings.
/// </summary>
/// <remarks>
/// JVMVRead returns a list of available workout videos.
/// The RawKey contains the date and optionally the horse's registration ID.
/// </remarks>
type WorkoutVideoListing =
    {
        /// <summary>Raw key string from JVMVRead.</summary>
        RawKey: string
        /// <summary>Workout date parsed from the key, if available.</summary>
        WorkoutDate: DateTime option
        /// <summary>Horse registration ID (血統登録番号), if present in the key.</summary>
        RegistrationId: string option
    }

/// <summary>Functions for working with WorkoutVideoListing values.</summary>
module WorkoutVideoListing =
    /// <summary>Attempts to parse a date from the first 8 characters of a key.</summary>
    let private tryParseDate (raw: string) =
        if String.IsNullOrWhiteSpace raw || raw.Length < 8 then
            None
        else
            let datePart = raw.Substring(0, 8)

            match DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | true, dt -> Some dt
            | _ -> None

    /// <summary>
    /// Parses a raw key string into a WorkoutVideoListing.
    /// </summary>
    /// <param name="rawKey">The raw key string from JVMVRead.</param>
    /// <returns>A WorkoutVideoListing with parsed date and registration ID.</returns>
    let parse (rawKey: string) =
        let trimmed = rawKey.TrimEnd([| '\n'; '\r' |]).TrimEnd([| char 0 |]).Trim()

        let registrationId =
            if trimmed.Length > 8 then
                let suffix = trimmed.Substring(8).Trim()

                if String.IsNullOrWhiteSpace suffix then
                    None
                else
                    Some suffix
            else
                None

        { RawKey = trimmed
          WorkoutDate = tryParseDate trimmed
          RegistrationId = registrationId }

/// <summary>
/// Represents the outcome of reading from JVMVRead.
/// </summary>
/// <remarks>
/// JVMVRead returns records one at a time until all listings are exhausted.
/// MovieEnd indicates no more records are available.
/// </remarks>
type MovieReadOutcome =
    /// <summary>A workout video listing record was read.</summary>
    | MovieRecord of WorkoutVideoListing
    /// <summary>No more records available (end of listing).</summary>
    | MovieEnd

/// <summary>
/// Represents detailed availability states for JV-Link movie checks via JVMVCheck.
/// </summary>
/// <remarks>
/// JVMVCheck returns different status codes indicating video availability.
/// Errors are propagated via Result&lt;MovieAvailability, ComError&gt;.
/// </remarks>
type MovieAvailability =
    /// <summary>The requested movie is available for playback.</summary>
    | Available
    /// <summary>The movie exists but is currently unavailable.</summary>
    | Unavailable
    /// <summary>The requested movie was not found.</summary>
    | NotFound
