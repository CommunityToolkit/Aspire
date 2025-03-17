# CommunityToolkit.Aspire.Hosting.Azure.Dapr library

Provides extension methods and resource definitions for a .NET Aspire AppHost to configure Dapr resources.

## Getting started

### Install the package

In your AppHost project, install the .NET Aspire Dapr Hosting library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Azure.Dapr
```

## Usage example

Then, in the _Program.cs_ file of `AppHost`, add Dapr resources and consume the connection using the following methods:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var stateStore = builder.AddDaprStateStore("statestore");
var pubSub = builder.AddDaprPubSub("pubsub");

builder.AddProject<Projects.MyApp>("myapp")
       .WithDaprSidecar()
       .WithReference(stateStore)
       .WithReference(pubSub);

builder.Build().Run();
```

## Additional documentation

https://dapr.io/

## Notes

Note that Aspire is only tested for compatibility with the most recently stable version of Dapr.
Currently 1.15.3 for the Dapr runtime and 1.15.0 for the Dapr CLI.

## Feedback & contributing

https://github.com/dotnet/aspire
