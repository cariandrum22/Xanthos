namespace Xanthos.Interop

open System

/// Provides a globally configurable sink for COM diagnostics so that callers can route
/// low-level JV-Link traces to their own logging infrastructure.
module Diagnostics =

    let mutable private sink: (string -> unit) option = None

    /// Registers a diagnostic sink that will receive JV-Link messages.
    let register (logger: string -> unit) = sink <- Some logger

    /// Clears the configured diagnostic sink so messages fall back to console output.
    let clear () = sink <- None

    let internal emit (message: string) =
        match sink with
        | Some logger ->
            try
                logger message
            with _ ->
                ()
        | None ->
            try
                Console.WriteLine("[COM-DIAG] " + message)
            with _ ->
                ()
