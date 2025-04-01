# CommunityToolkit.Aspire.Hosting.k6 library

Provides extension methods and resource definitions for the .NET Aspire AppHost to support running [Grafana k6](https://k6.io/) containers.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.k6
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, add a k6 resource and consume the connection using the following methods:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var myService = builder.AddProject<Projects.MyService>();

var k6 = builder.AddK6("k6")
                .WithReference(myService);

builder.Build().Run();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-k6

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
