namespace Xanthos.Runtime

open System

/// Simple logger abstraction to avoid hard dependency on external logging packages.
type TraceLogger =
    { Info: string -> unit
      Warn: string -> unit
      Error: string -> unit
      Debug: string -> unit }

module TraceLogger =

    /// <summary>
    /// Logger implementation that suppresses all output.
    /// </summary>
    let silent =
        { Info = ignore
          Warn = ignore
          Error = ignore
          Debug = ignore }

    /// <summary>
    /// Writes log messages to the console with basic severity prefixes.
    /// </summary>
    let ofConsole () =
        { Info = fun message -> Console.WriteLine $"[INFO ] {message}"
          Warn = fun message -> Console.WriteLine $"[WARN ] {message}"
          Error = fun message -> Console.WriteLine $"[ERROR] {message}"
          Debug = fun message -> Console.WriteLine $"[DEBUG] {message}" }
