# CommunityToolkit.Aspire.Hosting.EventStore library

Provides extension methods and resource definitions for the .NET Aspire app host to support running [EventStore](https://www.eventstore.com) containers.

## Getting Started

### Install the package

In your app host project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.EventStore
```

### Example usage

Then, in the _Program.cs_ file of app host, add a EventStore resource and consume the connection using the following methods:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var eventstore = builder.AddEventStore("eventstore");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(eventstore);

builder.Build().Run();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-eventstore

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

