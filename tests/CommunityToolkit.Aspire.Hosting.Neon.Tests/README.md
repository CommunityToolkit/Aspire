# Neon integration tests

The Neon integration suite includes two categories:

- Deterministic integration tests that validate hosting/provisioner behavior locally.
- Live Neon API tests that call the real Neon service.

## Why opt-in for live tests

Running live Neon API tests requires a Neon API key, so those scenarios are opt-in.

## Enable locally

Set these environment variables before running live Neon API tests:

- `RUN_NEON_INTEGRATION_TESTS=1`
- `NEON_INTEGRATION_API_KEY=<your-neon-api-key>`

Optional:

- `NEON_INTEGRATION_PROJECT_NAME=aspire-neon-integration` (default)
- `NEON_INTEGRATION_EPHEMERAL_PREFIX=aspire-it-` (default)

Live tests cover both attach and provision flows. Many provision scenarios use ephemeral branches for isolation, while others validate non-ephemeral paths (for example, attach and branch restore/default-branch flows). If the API key lacks required permissions for a scenario, that scenario is skipped with a clear reason so the suite continues.

PowerShell example:

```powershell
$env:RUN_NEON_INTEGRATION_TESTS = "1"
$env:NEON_INTEGRATION_API_KEY = "<your-neon-api-key>"
dotnet test tests/CommunityToolkit.Aspire.Hosting.Neon.Tests/CommunityToolkit.Aspire.Hosting.Neon.Tests.csproj --filter "Category=NeonIntegration"
```

## Run Neon integration category tests

```powershell
dotnet test tests/CommunityToolkit.Aspire.Hosting.Neon.Tests/CommunityToolkit.Aspire.Hosting.Neon.Tests.csproj --filter "Category=NeonIntegration"
```

Behavior with `Category=NeonIntegration`:

- With `RUN_NEON_INTEGRATION_TESTS=1`: deterministic + live tests run.
- With `RUN_NEON_INTEGRATION_TESTS=0` (or unset): deterministic tests run; live tests skip with a clear message.
