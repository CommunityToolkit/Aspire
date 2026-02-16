# Session: Rust Alternative Tooling

**Date:** 2026-02-13  
**Requested by:** Aaron Powell  
**Implemented by:** Kaylee

## Summary

Implemented alternative Rust build tooling for issue #1048.

## Changes

- Added `WithCargoCommand(string command, params string[] args)` method for custom cargo commands
- Added `WithCargoInstall(string packageName, ...)` method for package installation
- Added `RustToolInstallerResource` for tool installation with dependency tracking
- Updated tests and README
- No breaking changes to existing `AddRustApp` API

## Key Decision

Method naming aligns with consensus from issue #1048 discussion:
- `WithCargoCommand` signals cargo command replacement (even when not cargo itself)
- `WithCargoInstall` mirrors `cargo install` CLI convention
- `binstall` flag uses `cargo binstall -y` for non-interactive pre-built binary installation
- Package name appended last in args to follow cargo CLI conventions
