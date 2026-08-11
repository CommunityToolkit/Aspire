# CommunityToolkit.Aspire.Hosting.Mosquitto

## Overview

This Aspire hosting integration runs [Eclipse Mosquitto](https://mosquitto.org/) in a container. Mosquitto is an open source MQTT message broker, so the resource can be referenced by any MQTT client integration. The integration exposes the MQTT endpoint on port 1883 (target port `tcp`) and a protocol-level health check so the broker only reports healthy once it actually accepts MQTT connections.

## Usage

### Example 1: Add a Mosquitto broker

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var mqtt = builder.AddMosquitto("mqtt");

builder.AddProject<Projects.MyApp>("myapp")
       .WithReference(mqtt)
       .WaitFor(mqtt);

builder.Build().Run();
```

The connection string injected into the referencing resource is the broker address in the form `mqtt://host:port`.

### Example 2: Pin the MQTT host port

```csharp
var mqtt = builder.AddMosquitto("mqtt", port: 1883);
```

### Example 3: Persist data with a volume or bind mount

```csharp
var mqtt = builder.AddMosquitto("mqtt")
                  .WithDataVolume();

// or

var mqtt = builder.AddMosquitto("mqtt")
                  .WithDataBindMount("./mosquitto-data");
```

### Example 4: Mount a custom configuration

By default the integration ships a `mosquitto.conf` that exposes the MQTT listener on all interfaces (`listener 1883`), allows anonymous connections, and persists state to `/mosquitto/data`. To customize the broker, override the configuration with your own `mosquitto.conf` at `/mosquitto/config`:

```csharp
var mqtt = builder.AddMosquitto("mqtt")
                  .WithContainerFiles("/mosquitto/config",
                      [new ContainerFile { Name = "mosquitto.conf", Contents = "listener 1883" }]);
```

## Endpoints

| Name  | Description                      |
| ----- | -------------------------------- |
| `tcp` | MQTT protocol endpoint (port 1883) |

## Upstream Image

This integration pins the `eclipse-mosquitto` image (from `docker.io`) to a specific version tag (`2.0.22`) rather than a floating tag. Mosquitto publishes immutable, fully-versioned tags; update the pinned tag to adopt newer releases.
