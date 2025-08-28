# CommunityToolkit.Aspire.Hosting.NodeJS.Extensions library

This integration contains extensions for the [Node.js hosting package](https://nuget.org/packages/Aspire.Hosting.NodeJs) for .NET Aspire, including support for alternative package managers (yarn and pnpm), as well as developer workflow improvements.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.NodeJS.Extensions
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Node.js resource, then call `AddYarnApp` or `AddPnpmApp`:

```csharp
builder.AddYarnApp("yarn-demo")
    .WithExternalHttpEndpoints();

builder.AddPnpmApp("pnpm-demo")
    .WithExternalHttpEndpoints();
```

### Package installation with custom flags

You can pass additional flags to package managers during installation:

```csharp
// npm with legacy peer deps support
builder.AddNpmApp("npm-app", "./path/to/app")
    .WithNpmPackageInstallation(useCI: false, configureInstaller =>
    {
        configureInstaller.WithArgs("--legacy-peer-deps");
    })
    .WithExternalHttpEndpoints();

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

