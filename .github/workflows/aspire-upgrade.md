---
on:
    workflow_dispatch:
permissions:
    contents: read
    issues: read
    pull-requests: read
engine: copilot
network:
    allowed:
        - defaults
        - dotnet
tools:
    github:
        toolsets: [default]
    edit:
    bash: true
safe-outputs:
    create-pull-request:
---

# aspire-upgrade

The target version prefix is **17.0**.

# aspire-upgrade

You are responsible for updating the Aspire version in our repo to the latest nightly build.

You are to use the NuGet feed https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet9/nuget/v3/index.json

Look for the latest nightly version that starts with the target version prefix above. Query `Aspire.Hosting.AppHost`, which all C# AppHosts reference, on the specified feed.

Here is an example command to find the latest version:

```bash
dotnet package search Aspire.Hosting.AppHost --prerelease --exact-match --source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet9/nuget/v3/index.json --format json
```

Parse the result for the latest version that matches the target version prefix. This will be the version you update to in the next steps.

## Step 1: Update `Directory.Build.props`

Update the following values in the root `Directory.Build.props` file:

```xml
<AspireMajorVersion>MAJOR_VERSION</AspireMajorVersion>
<AspireVersion>$(AspireMajorVersion).MINOR_VERSION.PATCH_VERSION-PREVIEW_VERSION</AspireVersion>
<AspirePreviewSuffix></AspirePreviewSuffix>
```

Where MAJOR_VERSION, MINOR_VERSION, PATCH_VERSION, and PREVIEW_VERSION are the respective parts of the version you found.

When using a nightly build, we want to blank out the `AspirePreviewSuffix` to avoid confusion with the actual preview versions.

## Step 2: Verify the C# AppHost package pins

C# AppHosts use `Microsoft.NET.Sdk`. Verify that `Aspire.Hosting.AppHost` and `Aspire.Hosting.Dotnet` in `Directory.Packages.props` both use `$(AspireVersion)`; do not add SDK pins to `global.json` or package versions to individual projects.

## Step 3: Update TypeScript AppHost `aspire.config.json` files

Search the entire repository for all `aspire.config.json` files (typically under `examples/` in directories ending with `.AppHost.TypeScript`). Each file has an `sdk.version` field that must be updated to the new version.

The `sdk.version` value should be the full version string (e.g., `17.0.0-preview.1.26528.13`).

Use a command like `find . -name "aspire.config.json"` to locate all files, then update the `"version"` value inside the `"sdk"` object in each one.

## Step 4: Validate the changes

Run `dotnet restore` at the repository root to verify the new version resolves correctly. Fix any errors before proceeding.

## Step 5: Create a pull request

After all changes are made and validated, create a pull request with the title "Update Aspire version to X.Y.Z" where X.Y.Z is the full version you updated to.
