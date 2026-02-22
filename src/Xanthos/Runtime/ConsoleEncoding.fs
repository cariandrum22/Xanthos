namespace Xanthos.Runtime

open System
open System.IO
open System.Text

/// UTF-8 helpers for console apps and test harnesses.
///
/// Xanthos treats all in-memory text as Unicode strings (UTF-16). JV-Link boundaries
/// decode CP932/Shift-JIS into those strings, and output boundaries should emit UTF-8.
module ConsoleEncoding =

    /// UTF-8 without BOM (preferred for logs / redirected output).
    let utf8NoBom = UTF8Encoding(false)

    /// Best-effort: configure stdout/stderr/input to use UTF-8.
    ///
    /// - Works for redirected output (pipes) and most modern Windows consoles.
    /// - Errors are swallowed to avoid crashing when no console is attached.
    let configureUtf8 () =
        // Stdout
        try
            let writer = new StreamWriter(Console.OpenStandardOutput(), utf8NoBom)
            writer.AutoFlush <- true
            Console.SetOut(writer)
        with _ ->
            ()

        // Stderr
        try
            let writer = new StreamWriter(Console.OpenStandardError(), utf8NoBom)
            writer.AutoFlush <- true
            Console.SetError(writer)
        with _ ->
            ()

        // Console encodings (may throw in some hosts)
        try
            Console.OutputEncoding <- utf8NoBom
        with _ ->
            ()

        try
            Console.InputEncoding <- utf8NoBom
        with _ ->
            ()
