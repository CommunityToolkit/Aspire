# CommunityToolkit.Aspire.Hosting.Jellyfin

## Overview

This Aspire integration runs [Jellyfin](https://jellyfin.org/), the free software media system, in a container.

The integration exposes a connection string in the form `Endpoint=http://<host>:<port>`, which can be parsed with `DbConnectionStringBuilder` (or by reading the `Endpoint`, `Host`, `Port`, and `Uri` connection properties published by the resource).

> Jellyfin stores its library database, users, plugins, and watch history under `/config`. `AddJellyfin` defaults the container lifetime to `ContainerLifetime.Persistent` so that state survives AppHost restarts. Call `.WithLifetime(ContainerLifetime.Session)` to opt out.

## Usage

### Example: minimal Jellyfin with persisted config and a media library

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var jellyfin = builder.AddJellyfin("jellyfin")
    .WithDataVolume()
    .WithCacheVolume()
    .WithMediaBindMount("D:/Media");

builder.Build().Run();
```

### Example: multiple media libraries and custom subtitle fonts

```csharp
var jellyfin = builder.AddJellyfin("jellyfin")
    .WithDataBindMount("./jellyfin/config")
    .WithCacheBindMount("./jellyfin/cache")
    .WithMediaBindMount("D:/Movies", target: "/movies")
    .WithMediaBindMount("D:/TV", target: "/tv")
    .WithFontsBindMount("./jellyfin/fonts")
    .WithPublishedServerUrl("http://media.example.com");
```

### Example: enable UDP autodiscovery and DLNA

```csharp
var jellyfin = builder.AddJellyfin("jellyfin")
    .WithDataVolume()
    .WithDiscoveryEndpoint()
    .WithDlnaEndpoint();
```

### Connection properties

The resource publishes the following connection properties:

| Property   | Example                  |
| ---------- | ------------------------ |
| `Endpoint` | `http://localhost:32768` |
| `Host`     | `localhost`              |
| `Port`     | `32768`                  |
| `Uri`      | `http://localhost:32768` |

## Container image

- Registry: `docker.io`
- Image: `jellyfin/jellyfin`
- Tag: `10.11` (pinned major.minor)
