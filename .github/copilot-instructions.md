# Copilot Instructions

## Shared Instructions

Shared Copilot instructions, skills and prompts are maintained centrally in the [.github](https://github.com/f2calv/.github) repository, under `.github/instructions/`, `.github/skills/` and `.github/prompts/`. They are deliberately not copied into this repository, so a change there takes effect everywhere without a pull request here.

To load them, clone that repository and either add it to this VS Code workspace, or link its folders into `~/.copilot/`. Its README explains both.

If those shared files are not visible, stop and tell the user rather than guessing the conventions — this repository depends on them.

Everything below is specific to this repository.

## Provider Compatibility

Keep EF Core runtime/design/tasks and Npgsql's EF provider aligned per target: EF 10.x with
Npgsql EF 10.x for `net10.0`, and EF 11.x with Npgsql EF 11.x for `net11.0`.
The 11.x packages target .NET 11 only; do not upgrade the retained .NET 10 assets to them.
Validate both targets and both database adapters when changing either family.
