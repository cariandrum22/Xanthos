# Xanthos

F# wrapper library for JRA-VAN JV-Link API.

## Overview

Xanthos provides a type-safe F# interface to the JRA-VAN JV-Link COM API,
enabling developers to access Japanese horse racing data in a modern,
functional programming style.

## Features

- **Type-safe API**: Leverage F#'s type system for safer JV-Link interactions
- **Stub mode**: Test without COM dependencies on any platform
- **Cross-platform**: Core library works on Windows, macOS, and Linux (COM features Windows-only)

## Text Encoding

- **In-memory**: Xanthos represents text as Unicode `string` (UTF-16).
- **JV-Link input boundary**: JV-Link returns CP932/Shift-JIS for “text”; Xanthos decodes it into `string` via `Xanthos.Core.Text`.
- **Output boundary**: console/log/file text is UTF-8 (no BOM). `samples/Xanthos.Cli` configures stdout/stderr accordingly.

## Quick Start

Target `net10.0-windows` and run x64 with JV-Link 5.0 x64 installed and registered. The portable `net10.0` target supports record parsing and deterministic tests.

```fsharp
open Xanthos

JvLink.withSession ConnectionOptions.Default (fun session ->
    JvLink.init "UNKNOWN" session
    |> Result.bind (fun () -> session |> JvLink.getVersion))
|> function
    | Ok version -> printfn "JV-Link %s" version
    | Error error -> eprintfn "%s: %s" error.Api error.Message
```

`JvLink` functions are curried with Session last; the owner releases COM and its STA on success, errors and consumer exceptions. `Records` provides the 38 official record parsers. The CLI selects real COM with `--com` and a deterministic backend with `--stub`.

See the [functional API contract](functional-api.md), [CLI guide](functional-cli.md), [event delivery](functional-events.md) and [record migration](record-migration.md).

## Documentation

- [API Reference](reference/xanthos-api.html) - Auto-generated from XML documentation
- [Architecture](https://github.com/cariandrum22/Xanthos/tree/main/design/architecture) - Design documents

## License

This project is licensed under the MIT License.
