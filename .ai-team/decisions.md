# Decisions

> Shared decision log. All agents read this before starting work. Scribe merges new decisions from the inbox.
### 2026-02-13: Rust integration extension method naming uses `WithCargoCommand` and `WithCargoInstall`
**By:** Kaylee
**What:** Named the alternative build tooling methods `WithCargoCommand(string command, params string[] args)` and `WithCargoInstall(string packageName, ...)` rather than `WithRustCommand` or other variants. `WithCargoCommand` replaces both the executable and args (clearing the default `["run"]`). `WithCargoInstall` creates a `RustToolInstallerResource` with `WaitForCompletion` dependency, supporting `binstall`, `version`, `locked`, and `features` parameters.
**Why:** Aligns with issue #1048 discussion consensus (Aaron + Odonno + atm1150). "CargoCommand" signals that the method swaps what was originally a `cargo` command, even when the replacement isn't cargo itself. "CargoInstall" mirrors the `cargo install` CLI subcommand. The `binstall` flag uses `cargo binstall -y` for non-interactive pre-built binary installation. Package name is appended last in args to follow cargo CLI conventions.
