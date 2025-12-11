# Design Documentation

This directory houses all of the internal documentation used to design, maintain, and test the Xanthos library. Each subdirectory has a focused purpose so contributors can quickly find the right level of detail.

## Directory Map

| Directory | Purpose |
|-----------|---------|
| `architecture/` | Target architecture, component deep dives, and ongoing design notes. |
| `specs/` | Markdown exports of the official JV-Link specifications (methods, properties, error codes, records). These are generated from the raw PDFs/XLS in `source-docs/`. |
| `source-docs/` | Vendor-provided documentation in its original format. Treat as read-only. |
| `tests/` | Testing strategy, CLI E2E scenarios, and coverage notes. Mirrors the structure of the `tests/` source folder. |
| `notes/` | Scratchpads and decision logs for topics that have not yet graduated to formal architecture docs. |

When adding new documents, update this file (or the relevant subdirectory README) with a short description so future contributors can discover the material.
