# CommunityToolkit.Aspire.Hosting.NodeJS.Extensions

[![CommunityToolkit.Aspire.Hosting.NodeJS.Extensions](https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions)](https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions/) | [![CommunityToolkit.Aspire.Hosting.NodeJS.Extensions (latest)](<https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions?label=nuget%20(preview)>)](https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions/absoluteLatest)

## Overview

This package provides some extensions on the .NET Aspire [NodeJS hosting package](https://nuget.org/packages/Aspire.Hosting.NodeJS) and adds support for:

-   Running [Vite](https://vitejs.dev/) applications
-   Running Node.js applications using [Yarn](https://yarnpkg.com/) and [pnpm](https://pnpm.io/)
-   Ensuring that the packages are installed before running the application (using the specified package manager)

## Usage

```csharp
using CommunityToolkit.Aspire.Hosting.NodeJS.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddViteApp("vite-demo")
    .WithNpmPackageInstallation();

builder.AddViteApp("yarn-demo", packageManager: "yarn")
    .WithYarnPackageInstallation();

builder.AddViteApp("pnpm-demo", packageManager: "pnpm")
    .WithPnpmPackageInstallation();

builder.Build().Run();
```
