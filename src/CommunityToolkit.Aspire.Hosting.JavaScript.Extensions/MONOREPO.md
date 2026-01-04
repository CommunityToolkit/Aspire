# Frontend Monorepo Support

This extension now provides built-in support for frontend monorepos using [Nx](https://nx.dev) and [Turborepo](https://turborepo.com), plus native package-manager workspaces with yarn and pnpm.

## The Problem

When using monorepos, multiple applications share the same `package.json` and workspace setup. Previously, if you tried to run multiple apps with individual package installers, you would encounter race conditions:

```csharp
// This causes race conditions - multiple installers for the same directory
var app1 = builder.AddYarnApp("app-1", "./Frontend", args: ["app1"])
    .WithYarnPackageInstallation();

var app2 = builder.AddYarnApp("app-2", "./Frontend", args: ["app2"])
    .WithYarnPackageInstallation();

var app3 = builder.AddYarnApp("app-3", "./Frontend", args: ["app3"])
    .WithYarnPackageInstallation();
```

## The Solution

Use the new monorepo-specific extension methods that create a shared package installer:

### Nx Support

```csharp
var nx = builder.AddNxApp("nx", workingDirectory: "../frontend")
    .WithYarn(install: true) // Single shared installer
    .WithPackageManagerLaunch();

var app1 = nx.AddApp("app1");
var app2 = nx.AddApp("app2", appName: "my-app-2"); // Custom app name for nx serve
var app3 = nx.AddApp("app3");
```

### Turborepo Support

```csharp
var turbo = builder.AddTurborepoApp("turbo", workingDirectory: "../frontend")
    .WithPnpm(install: true) // Single shared installer
    .WithPackageManagerLaunch();

var app1 = turbo.AddApp("app1");
var app2 = turbo.AddApp("app2", filter: "custom-filter"); // Custom filter
var app3 = turbo.AddApp("app3");
```

### Yarn and pnpm workspaces (native)

For projects that already use yarn or pnpm workspaces without Nx/Turbo, you can still get a shared installer and per-app execution helpers:

```csharp
var yarn = builder.AddYarnWorkspaceApp("yarn-workspace", workingDirectory: "../frontend");
yarn.AddApp("yarn-web", workspaceName: "web"); // Runs: yarn workspace web run dev

var pnpm = builder.AddPnpmWorkspaceApp("pnpm-workspace", workingDirectory: "../frontend");
pnpm.AddApp("pnpm-web", filter: "web"); // Runs: pnpm --filter web run dev
```

## Package Managers

-   `.WithYarn()` / `.AddYarnWorkspaceApp()` - uses yarn
-   `.WithPnpm()` / `.AddPnpmWorkspaceApp()` - uses pnpm

> **Note**: npm support (`AddNpmApp`, `WithNpmPackageInstallation`) is now provided by [Aspire.Hosting.JavaScript](https://www.nuget.org/packages/Aspire.Hosting.JavaScript) starting with Aspire 13.

### Configuring Package Manager for App Execution

Use `WithPackageManagerLaunch()` to configure which package manager command is used when running individual apps:

```csharp
// Auto-infer from package installer annotation
var nx = builder.AddNxApp("nx", workingDirectory: "../frontend")
    .WithYarn()
    .WithPackageManagerLaunch(); // Will use 'yarn' command

// Explicitly specify package manager (independent of installer)
var turbo = builder.AddTurborepoApp("turbo", workingDirectory: "../frontend")
    .WithPnpm()
    .WithPackageManagerLaunch("yarn"); // Uses 'yarn' despite pnpm installer

// Without WithPackageManagerLaunch - uses default commands
var nxDefault = builder.AddNxApp("nx-default", workingDirectory: "../frontend");
nxDefault.AddApp("app1"); // Runs: nx serve app1 (no package manager prefix)
```

**Command Generation Examples:**

-   `WithPackageManagerLaunch("yarn")` → `yarn nx serve app1` or `yarn turbo run dev --filter app1`
-   `WithPackageManagerLaunch("pnpm")` → `pnpm nx serve app1` or `pnpm turbo run dev --filter app1`
-   No `WithPackageManagerLaunch()` → `nx serve app1` or `turbo run dev --filter app1`

## How It Works

1. **Shared Installer**: The top-level resource (`NxResource` or `TurborepoResource`) manages a single package installation
2. **Individual Apps**: Each app resource (`NxAppResource` or `TurborepoAppResource`) waits for the shared installer to complete
3. **No Race Conditions**: Only one package installer runs per workspace directory

## Commands Generated

### Nx Apps

-   Command: `nx serve {appName}`
-   The `appName` parameter controls which app Nx serves

### Turborepo Apps

-   Command: `turbo run dev --filter {filter}`
-   The `filter` parameter controls which packages Turborepo builds/serves

## Migration

If you're currently using the workaround pattern:

```csharp
// Old workaround approach
var app1 = builder.AddYarnApp("app-1", "./Frontend", args: ["app1"])
    .WithYarnPackageInstallation();

var app2 = builder.AddYarnApp("app-2", "./Frontend", args: ["app2"])
    .WaitFor(app1); // Manual dependency

var app3 = builder.AddYarnApp("app-3", "./Frontend", args: ["app3"])
    .WaitFor(app1); // Manual dependency
```

You can migrate to:

```csharp
// New monorepo approach
var nx = builder.AddNxApp("nx", workingDirectory: "./Frontend")
    .WithYarn()
    .WithPackageManagerLaunch(); // Configure package manager for app execution

var app1 = nx.AddApp("app-1", appName: "app1");
var app2 = nx.AddApp("app-2", appName: "app2");
var app3 = nx.AddApp("app-3", appName: "app3");
```

This provides cleaner syntax and automatic dependency management.

## Key Configuration Options

### Package Installation vs App Execution

It's important to understand the difference between package installation and app execution:

-   **Package Installer** (`.WithYarnPackageInstaller()`, `.WithPnpmPackageInstaller()`) - Controls how packages are installed in the workspace
-   **Package Manager for Apps** (`.WithPackageManagerLaunch()`) - Controls which command is used to run individual apps

```csharp
var nx = builder.AddNxApp("nx", workingDirectory: "../frontend")
    .WithPnpm(install: true)       // Install packages with: pnpm install
    .WithPackageManagerLaunch("yarn");   // Run apps with: yarn nx serve app1

// This is valid - you can install with pnpm but run apps with yarn
```

