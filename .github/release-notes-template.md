## Summary

Xanthos {{version}} {{release purpose and the main user-visible result}}.

## Highlights

- {{Important changes for consumers; link the changelog for the full list.}}

## Compatibility and migration

{{State supported .NET targets, Windows/JV-Link requirements, breaking changes and
required upgrade steps. For a compatible patch, say so explicitly.}}

## Verification

- **Managed checks:** {{CI links and results; distinguish Fast/Coverage from native verification.}}
- **WindowsManaged:** {{SDK-free Windows boundary results.}}
- **Native COM:** {{Environment, passed/failed/skipped counts and evidence link, or an explicit not-run explanation.}}
- **Package consumption:** {{Consumer/target verification, or an explicit not-run explanation.}}
- **Tag signature:** {{Link to the verified signed release tag.}}

## Known limitations

{{List accepted SDK failures and deferred live/UI checks with evidence. State
explicitly when there are no newly known limitations; do not turn a failed or
unperformed check into a passing result.}}

## Links

- [NuGet package](https://www.nuget.org/packages/Xanthos/{{version}})
- [Changelog](https://github.com/cariandrum22/Xanthos/blob/v{{version}}/CHANGELOG.md)
- [Release verification]({{verification issue URL}})
- [Full comparison](https://github.com/cariandrum22/Xanthos/compare/v{{previous version}}...v{{version}})
