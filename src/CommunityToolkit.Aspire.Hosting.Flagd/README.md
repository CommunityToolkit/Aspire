# CommunityToolkit.Aspire.Hosting.Flagd

A .NET Aspire hosting integration for [flagd](https://flagd.dev), a feature flag evaluation engine that provides a ready-made, open source, OpenFeature-compliant feature flag backend system.

## Getting started

### Prerequisites

-   .NET 8.0 or later
-   Docker (for running the flagd container)

### Installation

Install the package by adding a PackageReference to your `AppHost` project:

```xml
<PackageReference Include="CommunityToolkit.Aspire.Hosting.Flagd" />
```

### Usage

In your `AppHost` project, call the `AddFlagd` method to add flagd to your application with a flag configuration file:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var flagd = builder
    .AddFlagd("flagd")
    .WithBindFileSync("./flags/")
    .WithLogging();

builder.Build().Run();
```

The `fileSource` parameter specifies the path to your flag configuration file on the host machine, which will be mounted into the flagd container.

Important: The `flagd` requires a Sync to be configured. You can use the `WithBindFileSync` method to configure a file sync. The `./flags/` path is the default path where the flag configuration file is expected to be found. You can change this path to match your configuration.

### Configuration

#### Configuring logging

You can enable the logging for flagd:

```csharp
var flagd = builder
    .AddFlagd("flagd")
    .WithBindFileSync("./flags")
    .WithLogging();
```

#### Customizing the port (flagd endpoint)

You can specify a custom port for the flagd HTTP endpoints:

```csharp
var flagd = builder.AddFlagd("flagd", port: 9090);
```

If no port is specified, the default port 8013 will be used.

#### Customizing the port (OFREP endpoint)

You can specify a custom port for the OFREP HTTP endpoints:

```csharp
var flagd = builder.AddFlagd("flagd", ofrepPort: 9090);
```

If no port is specified, the default port 8016 will be used.

### Flag Configuration Format

flagd uses JSON files for flag definitions. Please refer to the official documentation for more information. You can create
a folder named `flags` in your project root and place your `flagd.json` file inside it. It is mandatory for the flag configuration
file to be called `flagd.json`.

Here's a simple example:

```json
{
    "$schema": "https://flagd.dev/schema/v0/flags.json",
    "flags": {
        "welcome-banner": {
            "state": "ENABLED",
            "variants": {
                "on": true,
                "off": false
            },
            "defaultVariant": "off"
        },
        "background-color": {
            "state": "ENABLED",
            "variants": {
                "red": "#FF0000",
                "blue": "#0000FF",
                "yellow": "#FFFF00"
            },
            "defaultVariant": "red",
            "targeting": {
                "if": [
                    {
                        "===": [
                            {
                                "var": "company"
                            },
                            "aspire"
                        ]
                    },
                    "blue"
                ]
            }
        },
        "api-version": {
            "state": "ENABLED",
            "variants": {
                "v1": "1.0",
                "v2": "2.0",
                "v3": "3.0"
            },
            "defaultVariant": "v1"
        }
    }
}
```

## Additional Information

For more information about flagd, visit the [official documentation](https://flagd.dev).

To use flagd in your application, you'll need to install an OpenFeature provider for .NET. See the [OpenFeature .NET documentation](https://openfeature.dev/docs/reference/technologies/client/dotnet/) for details.

