namespace Xanthos

open Xanthos.Core

/// <summary>
/// Convenience helpers for constructing domain types.
/// </summary>
[<AutoOpen>]
module Api =

    /// <summary>
    /// Convenience helper that constructs a <see cref="RaceInfo"/> with sensible defaults.
    /// </summary>
    let createRace name =
        match RaceId.create name with
        | Ok raceId ->
            Ok
                { Id = raceId
                  Name = name
                  Course = None
                  DistanceMeters = None
                  Surface = TrackSurface.UnknownSurface
                  Condition = TrackCondition.UnknownCondition
                  ScheduledStart = None }
        | Error error -> Error error
