# Frontend Monorepo Support

This extension now provides built-in support for frontend monorepos using [Nx](https://nx.dev) and [Turborepo](https://turborepo.com).

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
    .WithNpmPackageInstaller(); // Single shared installer

var app1 = nx.AddApp("app1");
var app2 = nx.AddApp("app2", appName: "my-app-2"); // Custom app name for nx serve
var app3 = nx.AddApp("app3");
```

### Turborepo Support

```csharp
var turbo = builder.AddTurborepoApp("turbo", workingDirectory: "../frontend")
    .WithYarnPackageInstaller(); // Single shared installer

var app1 = turbo.AddApp("app1");
var app2 = turbo.AddApp("app2", filter: "custom-filter"); // Custom filter
var app3 = turbo.AddApp("app3");
```

## Package Managers

Both Nx and Turborepo support all three package managers:

- `.WithNpmPackageInstaller()` - uses npm (supports `useCI` parameter)
- `.WithYarnPackageInstaller()` - uses yarn
- `.WithPnpmPackageInstaller()` - uses pnpm

## How It Works

1. **Shared Installer**: The top-level resource (`NxResource` or `TurborepoResource`) manages a single package installation
2. **Individual Apps**: Each app resource (`NxAppResource` or `TurborepoAppResource`) waits for the shared installer to complete
3. **No Race Conditions**: Only one package installer runs per workspace directory

## Commands Generated

### Nx Apps
- Command: `nx serve {appName}`
- The `appName` parameter controls which app Nx serves

### Turborepo Apps  
- Command: `turbo run dev --filter {filter}`
- The `filter` parameter controls which packages Turborepo builds/serves

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
    .WithYarnPackageInstaller();

var app1 = nx.AddApp("app-1", appName: "app1");
var app2 = nx.AddApp("app-2", appName: "app2");
var app3 = nx.AddApp("app-3", appName: "app3");
```

This provides cleaner syntax and automatic dependency management.