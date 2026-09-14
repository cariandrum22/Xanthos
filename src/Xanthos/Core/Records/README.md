# Pre-migration compatibility code

These modules now belong to `Xanthos.Legacy.Records`. They preserve the old Xanthos models and synthetic parsing behavior for migration reference. Several layouts and record meanings are known to disagree with JV-Data; do not use them to decode SDK responses.

Use `Xanthos.Records.parse` or `Xanthos.Records.parseRA` and the other record functions. `Xanthos.Runtime.PayloadParser` delegates to that canonical interface. Unsupported official records return a located error until migrated; unknown IDs retain their original bytes. H5 is not an official record type.

Tests that import the Legacy namespace describe compatibility behavior, not compliance with the SDK. The independent `*ContractTests` and `Contracts/record-layouts.json` verify the official projections.
