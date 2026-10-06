---
name: Integration tests
description: Use when creating or modifying test projects and test code.
applyTo: "tests/**"
---

# Test guidance

- Tests use xUnit and should follow the patterns in the nearest matching test project.
- Mark tests that require Linux containers with `[RequiresDocker]`.
- Prefer Aspire health and resource notification APIs for readiness. Use `WaitForTextAsync` only when the resource has no structured readiness signal.
- Test public behavior and meaningful app-model output, not only implementation details.
- Avoid fixed ports and shared mutable process state unless the test specifically covers them.
- Run the narrowest relevant project or test filter first.
- When adding a test project, add it to `CommunityToolkit.Aspire.slnx` and verify discovery with `./eng/testing/generate-test-list-for-workflow.sh --json`. The reusable matrix workflow is `.github/workflows/tests.yaml`.
