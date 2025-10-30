# CommunityToolkit.Aspire.Hosting.NodeJS.Extensions library

This integration contains extensions for the [Node.js hosting package](https://nuget.org/packages/Aspire.Hosting.NodeJs) for .NET Aspire, including support for alternative package managers (yarn and pnpm) and frontend monorepos (Nx, Turborepo).

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.NodeJS.Extensions
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Node.js resource, then call `AddYarnApp` or `AddPnpmApp`:

```csharp
builder.AddYarnApp("yarn-demo", "../yarn-demo")
    .WithExternalHttpEndpoints();

builder.AddPnpmApp("pnpm-demo", "../pnpm-demo")
    .WithExternalHttpEndpoints();
```

### Frontend Monorepo Support

For Nx and Turborepo monorepos, use the dedicated monorepo methods to avoid package installation race conditions:

```csharp
// Nx workspace
var nx = builder.AddNxApp("nx", workingDirectory: "../frontend")
    .WithYarnPackageInstaller()
    .RunWithPackageManager(); // Automatically uses yarn from installer

var app1 = nx.AddApp("app1");
var app2 = nx.AddApp("app2", appName: "my-app-2");

// Turborepo workspace  
var turbo = builder.AddTurborepoApp("turbo", workingDirectory: "../frontend")
    .WithPnpmPackageInstaller()
    .RunWithPackageManager("pnpm"); // Explicitly specify pnpm

var turboApp1 = turbo.AddApp("app1");
var turboApp2 = turbo.AddApp("app2", filter: "custom-filter");
```

See [MONOREPO.md](./MONOREPO.md) for detailed documentation on monorepo support.

### Configuring Package Manager for Monorepos

The `RunWithPackageManager()` method configures which package manager command is used when running individual apps in Nx or Turborepo workspaces:

```csharp
// Auto-infer from package installer
var nx = builder.AddNxApp("nx", workingDirectory: "../frontend")
    .WithYarnPackageInstaller()
    .RunWithPackageManager(); // Uses 'yarn' command

// Explicitly specify package manager
var turbo = builder.AddTurborepoApp("turbo", workingDirectory: "../frontend")
    .WithPnpmPackageInstaller()
    .RunWithPackageManager("pnpm"); // Uses 'pnpm' command

// Generated commands:
// Nx with yarn: yarn nx serve app1
// Turborepo with pnpm: pnpm turbo run dev --filter app1
```

### Package installation with custom flags

You can pass additional flags to package managers during installation:

```csharp
// yarn with frozen lockfile
builder.AddYarnApp("yarn-app", "./path/to/app")  
    .WithYarnPackageInstallation(configureInstaller =>
    {
        configureInstaller.WithArgs("--frozen-lockfile", "--verbose");
    })
    .WithExternalHttpEndpoints();

// pnpm with frozen lockfile
builder.AddPnpmApp("pnpm-app", "./path/to/app")
    .WithPnpmPackageInstallation(configureInstaller =>
    {
        configureInstaller.WithArgs("--frozen-lockfile");
    })
    .WithExternalHttpEndpoints();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-nodejs-extensions

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

