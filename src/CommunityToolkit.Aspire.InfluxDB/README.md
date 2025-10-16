# CommunityToolkit.Aspire.InfluxDB

Registers an [InfluxDBClient](https://github.com/influxdata/influxdb-client-csharp) in the DI container for connecting to an InfluxDB instance.

## Getting started

### Prerequisites

- InfluxDB 2.x server or cloud instance.

### Install the package

Install the .NET Aspire InfluxDB Client library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.InfluxDB
```

## Usage example

In the _Program.cs_ file of your project, call the `AddInfluxDBClient` extension method to register an `InfluxDBClient` for use via the dependency injection container. The method takes a connection name parameter.

```csharp
builder.AddInfluxDBClient("influxdb");
```

Then, in your service, inject `InfluxDBClient` and use it to interact with the InfluxDB instance:

```csharp
public class MyService(InfluxDBClient client)
{
    // Use the client to write or query data
}
```

## Configuration

The .NET Aspire InfluxDB Client integration provides multiple options to configure the server connection based on the requirements and conventions of your project.

### Use a connection string

When using a connection string from the `ConnectionStrings` configuration section, you can provide the name of the connection string when calling `builder.AddInfluxDBClient()`:

```csharp
builder.AddInfluxDBClient("influxdb");
```

And then the connection string will be retrieved from the `ConnectionStrings` configuration section:

```json
{
    "ConnectionStrings": {
        "influxdb": "Url=http://localhost:8086;Token=my-token;Organization=my-org;Bucket=my-bucket"
    }
}
```

### Use configuration providers

The .NET Aspire InfluxDB Client integration supports [Microsoft.Extensions.Configuration](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration). It loads the `InfluxDBClientSettings` from configuration by using the `Aspire:InfluxDB:Client` key. Example `appsettings.json` that configures some of the options:

```json
{
  "Aspire": {
    "InfluxDB": {
      "Client": {
        "Url": "http://localhost:8086",
        "Token": "my-token",
        "Organization": "my-org",
        "Bucket": "my-bucket"
      }
    }
  }
}
```

### Use inline delegates

Also you can pass the `Action<InfluxDBClientSettings> configureSettings` delegate to set up some or all the options inline, for example to set the token from code:

```csharp
builder.AddInfluxDBClient("influxdb", settings => 
{
    settings.Token = "my-token";
    settings.Organization = "my-org";
    settings.Bucket = "my-bucket";
});
```

## AppHost extensions

In your AppHost project, register an InfluxDB container and consume the connection using the following methods:

```csharp
var influxdb = builder.AddInfluxDB("influxdb");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(influxdb);
```

## Additional documentation

- https://github.com/influxdata/influxdb-client-csharp
- https://docs.influxdata.com/influxdb/latest/tools/client-libraries/

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
