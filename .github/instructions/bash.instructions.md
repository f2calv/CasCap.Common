---
description: 'Bash scripting conventions — structure, error handling, logging and testability.'
applyTo: '**/*.sh'
---

# Bash / Shell Scripts

## Structure

- **Shebang**: `#!/usr/bin/env bash` (not `#!/bin/bash`) — resolves bash via `PATH` rather than assuming it lives at `/bin/bash`.
- **Header comment**: Every script opens with a short comment block stating what it does and, when it's invoked from a GitHub Actions composite step, a `# Required environment variables: X, Y, Z` line (plus a note on which are optional). This is the script's contract — a reader shouldn't need to open the calling `action.yml` to know what it needs.
- **Location**: Non-trivial or GitHub Actions composite-action scripts live in a dot-prefixed `.scripts/` folder at the repository (or action) root, per `github-actions.instructions.md`.
- **Executable bit**: Set `chmod +x` on any script invoked directly by path (e.g. `run: "${{ github.action_path }}/.scripts/name.sh"`) rather than via `bash script.sh`. **Gotcha**: this container's `git config core.fileMode` is `false`, so `git add` on a newly created file does not pick up a `chmod +x` applied before staging. Verify with `git ls-files -s path/to/script.sh` shows `100755`; if it shows `100644`, fix it with `git update-index --chmod=+x path/to/script.sh` before committing.
- **Exemption — manual/interactive runbooks**: A script meant to be read and run step-by-step by a human (contains `sudo reboot`, `sudo nano`, or similar interactive/destructive steps) is exempt from the automation conventions below. Mark it as such in its header comment (e.g. `# Run manually, step by step — not intended for unattended execution.`) so it isn't mistaken for broken automation.

## Error Handling

- **Validate required environment variables before enabling strict mode**, so a missing variable produces one clear, intentional error instead of bash's raw "unbound variable" trace:

  ```bash
  # Validate required environment variables before enabling strict mode
  for var in FOO BAR BAZ; do
    if [[ -z "${!var:-}" ]]; then
      echo "::error::Required environment variable $var is not set."
      exit 1
    fi
  done

  set -euo pipefail
  ```

- **`set -euo pipefail`** is required in every automation script (exit on error, exit on unset variable, fail a pipeline on any stage's non-zero exit), placed immediately after the required-variable validation loop above.
- **Default optional variables explicitly** before `set -u` takes effect, using `: "${VAR:=}"` (or a per-use `"${VAR:-}"`), so referencing an optional, legitimately-unset variable doesn't crash the script.
- **Quote every variable expansion** (`"$var"`, `"${arr[@]}"`) unless intentional word-splitting/globbing is required — this includes file paths, which may contain spaces.
- **Fail fast with a specific message** rather than letting an empty/invalid value propagate into a downstream command's cryptic error (e.g. validate chart registry credentials before calling `helm pull`, not after it fails).

## Logging & GitHub Actions Annotations

- Use `::error::message` (or `::error file=$FILE::message` when a specific file is implicated) for failures, `::warning title=X::message` for non-fatal issues, and `::add-mask::$value` for any secret obtained or derived *at runtime* (an exchanged token, a short-lived API key) — not just secrets passed in as inputs.
- Log plain progress/diagnostic lines with `echo`; prefer one `echo` per line over `printf "\n...` continuation chains for readability of the raw log.
- Never `echo`/log a secret value in full — redact it (e.g. `echo "CHART_REGISTRY_PASSWORD=***"`) or rely on `::add-mask::`.

## Testability

- **A script extracted per `github-actions.instructions.md`'s Composite Actions guidance must be runnable and testable standalone**, without a live Actions run. Depend only on the documented environment variables — nothing implicit from the Actions runtime beyond `GITHUB_ENV`/`GITHUB_OUTPUT` (which can be pointed at a temp file locally).
- **Mock external commands** (`curl`, `helm`, `git`, cloud CLIs) for local testing by placing a stub executable earlier on `PATH` rather than hitting real endpoints or requiring live credentials. Exercise the success path and every distinct error path (missing required variable, empty/invalid response, etc.).
- Run `shellcheck` against new or modified scripts before committing; treat anything above informational severity as worth fixing (an informational `SC2016` on an intentionally single-quoted `yq`/`jq` expression, for example, is expected and can be left as-is).

## Style

- **`local`** every function-scoped variable.
- Lowercase values that feed case-sensitive downstream systems expecting lowercase (OCI registries/repositories) with `${var,,}`.
- Clean up any temporary file/directory a script creates, even when it runs in a loop (each iteration's temp artifacts must not leak into the next).
