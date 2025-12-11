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

## Quick Start

```fsharp
open System
open Xanthos.Runtime
open Xanthos.Interop

// Create configuration
let config =
    { Sid = "YOUR_SID"
      SavePath = Some @"C:\JVData"
      ServiceKey = None
      UseJvGets = None }

let request =
    { Spec = "RACE"
      FromTime = DateTime.Today.AddDays(-7.0)
      Option = 1 }

// IMPORTANT: JvLinkService takes ownership of the client and MUST be disposed.
// Use the 'use' keyword to ensure proper cleanup of COM resources.
use service = new JvLinkService(new ComJvLinkClient(), config)

// Fetch data
match service.FetchPayloads(request) with
| Ok payloads -> printfn "Fetched %d payloads" payloads.Length
| Error err -> printfn "Error: %A" err

// Service and client are automatically disposed when leaving scope
```

> **Note:** For testing without JV-Link installed, use `JvLinkStub()` instead of `ComJvLinkClient()`.

## Documentation

- [API Reference](reference/xanthos-api.html) - Auto-generated from XML documentation
- [Architecture](https://github.com/cariandrum22/Xanthos/tree/main/design/architecture) - Design documents

## License

This project is licensed under the MIT License.
