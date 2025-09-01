# CommunityToolkit.Aspire.Hosting.Flagd

A .NET Aspire hosting integration for [flagd](https://flagd.dev), a feature flag evaluation engine that provides a ready-made, open source, OpenFeature-compliant feature flag backend system.

## Getting started

### Prerequisites

- .NET 8.0 or later
- Docker (for running the flagd container)

### Installation

Install the package by adding a PackageReference to your `AppHost` project:

```xml
<PackageReference Include="CommunityToolkit.Aspire.Hosting.Flagd" />
```

### Usage

In your `AppHost` project, call the `AddFlagd` method to add flagd to your application:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var flagd = builder.AddFlagd("flagd");

builder.Build().Run();
```

### Configuration

You can configure flagd with various options:

#### Using a flag configuration file

```csharp
var flagd = builder.AddFlagd("flagd")
    .WithFlagConfigurationFile("./flags.json");
```

#### Using HTTP sync

```csharp
var flagd = builder.AddFlagd("flagd")
    .WithHttpSync("http://example.com/flags.json", interval: 10);
```

#### Adding multiple flag sources

```csharp
var flagd = builder.AddFlagd("flagd")
    .WithFlagSource("file:///etc/flagd/flags1.json")
    .WithFlagSource("http://example.com/flags2.json");
```

#### Configuring logging

```csharp
var flagd = builder.AddFlagd("flagd")
    .WithLogging("debug");
```

#### Adding persistent storage

```csharp
var flagd = builder.AddFlagd("flagd")
    .WithDataVolume();
```

### Flag Configuration Format

flagd uses JSON files for flag definitions. Here's a simple example:

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
        "green": "#00FF00"
      },
      "defaultVariant": "red",
      "targeting": {
        "if": [
          {
            "===": [
              {
                "var": "user.company"
              },
              "acme"
            ]
          },
          "blue"
        ]
      }
    }
  }
}
```

## Additional Information

For more information about flagd, visit the [official documentation](https://flagd.dev).

To use flagd in your application, you'll need to install an OpenFeature provider for .NET. See the [OpenFeature .NET documentation](https://openfeature.dev/docs/reference/technologies/client/dotnet/) for details.
