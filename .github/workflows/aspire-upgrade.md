---
description: Updates Aspire SDK to the latest nightly build
on:
    workflow_dispatch:
concurrency: aspire-upgrade
permissions:
    contents: read
network:
    allowed:
        - defaults
tools:
    github:
        toolsets: [repos, context]
    edit:
    bash: true
    web-fetch:
safe-outputs:
    create-pull-request:
---

The target version prefix is **13.2**.

# aspire-upgrade

You are responsible for updating the Aspire version in our repo to the latest nightly build.

You are to use the NuGet feed https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet9/nuget/v3/index.json

Look for the latest nightly version that starts with the target version prefix above.

## Step 1: Update `Directory.Build.props`

Update the following values in the root `Directory.Build.props` file:

```xml
<AspireMajorVersion>MAJOR_VERSION</AspireMajorVersion>
<AspireVersion>$(AspireMajorVersion).MINOR_VERSION.PATCH_VERSION-PREVIEW_VERSION</AspireVersion>
<AspirePreviewSuffix></AspirePreviewSuffix>
```

Where MAJOR_VERSION, MINOR_VERSION, PATCH_VERSION, and PREVIEW_VERSION are the respective parts of the version you found.

When using a nightly build, we want to blank out the `AspirePreviewSuffix` to avoid confusion with the actual preview versions.

## Step 2: Update all AppHost project files

Search the entire repository for **all** `.csproj` files whose `<Project Sdk="...">` attribute references `Aspire.AppHost.Sdk`. There are many of these across `tests/`, `tests-app-hosts/`, and `examples/`.

Each one must be updated to the full version string:

```xml
<Project Sdk="Aspire.AppHost.Sdk/MAJOR_VERSION.MINOR_VERSION.PATCH_VERSION-PREVIEW_VERSION">
```

**We cannot use a MSBuild variable in the Project SDK attribute, so you must hardcode the version in every file.**

Use a command like `grep -rl "Aspire.AppHost.Sdk" --include="*.csproj"` to find all files that need updating.

## Step 3: Validate the changes

Run `dotnet restore` at the repository root to verify the new version resolves correctly. Fix any errors before proceeding.

## Step 4: Create a pull request

After all changes are made and validated, create a pull request with the title "Update Aspire version to X.Y.Z" where X.Y.Z is the full version you updated to.
