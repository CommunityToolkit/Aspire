# CommunityToolkit.Aspire.EventStore

Registers an [EventStoreClient](https://github.com/EventStore/EventStore-Client-Dotnet) in the DI container for connecting to an EventStore.

## Getting started

### Prerequisites

-   EventStore cluster.

### Install the package

Install the .NET Aspire EventStore Client library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.EventStore
```

## Usage example

In the _Program.cs_ file of your project, call the `AddEventStoreClient` extension method to register an `EventStoreClient` for use via the dependency injection container. The method takes a connection name parameter.

```csharp
builder.AddEventStoreClient("eventstore");
```

## Configuration

The .NET Aspire EventStore Client integration provides multiple options to configure the server connection based on the requirements and conventions of your project.

### Use a connection string

When using a connection string from the `ConnectionStrings` configuration section, you can provide the name of the connection string when calling `builder.AddEventStoreClient()`:

```csharp
builder.AddEventStoreClient("eventstore");
```

And then the connection string will be retrieved from the `ConnectionStrings` configuration section:

```json
{
    "ConnectionStrings": {
        "eventstore": "esdb://localhost:22113?tls=false"
    }
}
```

### Use configuration providers

The .NET Aspire EventStore Client integration supports [Microsoft.Extensions.Configuration](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration). It loads the `EventStoreSettings` from configuration by using the `Aspire:EventStore:Client` key. Example `appsettings.json` that configures some of the options:

```json
{
  "Aspire": {
    "EventStore": {
      "Client": {
        "ConnectionString": "esdb://localhost:22113?tls=false",
        "DisableHealthChecks": true
      }
    }
  }
}
```

### Use inline delegates

Also you can pass the `Action<EventStoreClientSettings> configureSettings` delegate to set up some or all the options inline, for example to set the API key from code:

```csharp
builder.AddEventStoreClient("eventstore", settings => settings.DisableHealthChecks = true);
```

## AppHost extensions

In your AppHost project, install the `CommunityToolkit.Aspire.Hosting.EventStore` library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.EventStore
```

Then, in the _Program.cs_ file of `AppHost`, register EventStore and consume the connection using the following methods:

```csharp
var eventstore = builder.AddEventStore("eventstore");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(eventstore);
```

The `WithReference` method configures a connection in the `MyService` project named `eventstore`. In the _Program.cs_ file of `MyService`, the EventStore connection can be consumed using:

```csharp
builder.AddEventStoreClient("eventstore");
```

Then, in your service, inject `EventStoreClient` and use it to interact with the EventStore API:

```csharp
public class MyService(EventStoreClient eventStoreClient)
{
    // ...
}
```

## Additional documentation

-   https://github.com/EventStore/EventStore-Client-Dotnet
-   https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-eventstore

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

