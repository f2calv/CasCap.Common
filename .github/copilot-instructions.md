# Copilot Instructions

## Shared Instructions

Shared Copilot instructions, skills and prompts are maintained centrally in the [.github](https://github.com/f2calv/.github) repository, under `.github/instructions/`, `.github/skills/` and `.github/prompts/`. They are deliberately not copied into this repository, so a change there takes effect everywhere without a pull request here.

To load them, clone that repository and either add it to this VS Code workspace, or link its folders into `~/.copilot/`. Its README explains both.

If those shared files are not visible, stop and tell the user rather than guessing the conventions — this repository depends on them.

Everything below is specific to this repository.

## Architecture and Namespaces

This repository follows the `capability-library-suite` architecture in the shared
[architecture-patterns skill](https://github.com/f2calv/.github/blob/main/.github/skills/patterns-architecture/SKILL.md):
its published packages supply capabilities composed by consuming applications, not a deployed
modular monolith.

The approved public-library alignment target is `flattened-role-namespaces` with root
`CasCap.Common`, as defined by the shared
[.NET namespace policy](https://github.com/f2calv/.github/blob/main/.github/skills/patterns-architecture/references/dotnet-namespaces.md).
Existing capability/provider-scoped configuration APIs and framework-owned namespaces remain
current deviations for the architecture review; this declaration does not claim completed
alignment or approve breaking API changes. Test suites and executable test hosts are outside this
namespace review; the published `CasCap.Common.Testing` support library remains in scope.

## Provider Compatibility

Keep EF Core runtime/design/tasks and Npgsql's EF provider aligned per target: EF 10.x with
Npgsql EF 10.x for `net10.0`, and EF 11.x with Npgsql EF 11.x for `net11.0`.
The 11.x packages target .NET 11 only; do not upgrade the retained .NET 10 assets to them.
Validate both targets and both database adapters when changing either family.
