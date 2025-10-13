# CommunityToolkit.Aspire.Hosting.InfluxDB

This package provides InfluxDB support for .NET Aspire, allowing you to easily add an InfluxDB container to your distributed application.

## Getting started

### Install the package

Install the .NET Aspire InfluxDB Hosting library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.InfluxDB
```

## Usage example

In the _Program.cs_ file of your AppHost project, add an InfluxDB container:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var influxdb = builder.AddInfluxDB("influxdb");

var api = builder.AddProject<Projects.MyService>()
                 .WithReference(influxdb);

builder.Build().Run();
```

## Configuration

The InfluxDB container is configured with the following default settings:

- Port: 8086
- Image: `influxdb:2.7`
- Default organization: `default`
- Default bucket: `default`

### Data persistence

You can add data persistence to the InfluxDB container using volumes or bind mounts:

#### Using a volume

```csharp
var influxdb = builder.AddInfluxDB("influxdb")
    .WithDataVolume();
```

#### Using a bind mount

```csharp
var influxdb = builder.AddInfluxDB("influxdb")
    .WithDataBindMount("./data/influxdb/data");
```

### Custom credentials

You can provide custom credentials for the InfluxDB container:

```csharp
var username = builder.AddParameter("influxdb-username", secret: false);
var password = builder.AddParameter("influxdb-password", secret: true);
var token = builder.AddParameter("influxdb-token", secret: true);

var influxdb = builder.AddInfluxDB("influxdb", username, password, token);
```

## Additional documentation

- https://docs.influxdata.com/influxdb/latest/
- https://github.com/influxdata/influxdb-client-csharp

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
