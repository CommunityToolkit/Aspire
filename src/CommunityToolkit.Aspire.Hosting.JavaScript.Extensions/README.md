# CommunityToolkit.Aspire.Hosting.JavaScript.Extensions library

This integration contains extensions for the [Node.js hosting package](https://nuget.org/packages/Aspire.Hosting.JavaScript) for Aspire, including support for frontend monorepos (Nx, Turborepo) and native package-manager workspaces (yarn, pnpm).

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.JavaScript.Extensions
```

### Example usage

For Nx, Turborepo, and package-manager workspaces, use the dedicated helpers to avoid package installation race conditions:

```csharp
// Nx workspace
var nx = builder.AddNxApp("nx", workingDirectory: "../frontend")
    .WithYarn()
    .WithPackageManagerLaunch(); // Automatically uses yarn from installer

var app1 = nx.AddApp("app1");
var app2 = nx.AddApp("app2", appName: "my-app-2");

// Turborepo workspace
var turbo = builder.AddTurborepoApp("turbo", workingDirectory: "../frontend")
    .WithPackageManagerLaunch("pnpm"); // Explicitly specify pnpm

var turboApp1 = turbo.AddApp("app1");
var turboApp2 = turbo.AddApp("app2", filter: "custom-filter");

// Yarn workspace (package-manager native)
var yarn = builder.AddYarnWorkspaceApp("yarn-workspace", workingDirectory: "../frontend", install: true);
yarn.AddApp("yarn-web", workspaceName: "web");

// pnpm workspace (package-manager native)
var pnpm = builder.AddPnpmWorkspaceApp("pnpm-workspace", workingDirectory: "../frontend", install: true);
pnpm.AddApp("pnpm-web", filter: "web");
```

See [MONOREPO.md](./MONOREPO.md) for detailed documentation on monorepo support.

### Configuring Package Manager for Monorepos

The `WithPackageManagerLaunch()` method configures which package manager command is used when running individual apps in Nx or Turborepo workspaces:

```csharp
// Auto-infer from package installer
var nx = builder.AddNxApp("nx", workingDirectory: "../frontend")
    .WithYarn()
    .WithPackageManagerLaunch(); // Uses 'yarn' command

// Explicitly specify package manager
var turbo = builder.AddTurborepoApp("turbo", workingDirectory: "../frontend")
    .WithPnpm()
    .WithPackageManagerLaunch("pnpm"); // Uses 'pnpm' command

// Generated commands:
// Nx with yarn: yarn nx serve app1
// Turborepo with pnpm: pnpm turbo run dev --filter app1
// Yarn workspace app: yarn workspace app1 run dev
// pnpm workspace app: pnpm --filter app1 run dev
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

