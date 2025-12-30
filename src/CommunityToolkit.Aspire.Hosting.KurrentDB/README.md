# CommunityToolkit.Aspire.Hosting.KurrentDB library

Provides extension methods and resource definitions for the .NET Aspire app host to support running [KurrentDB](https://www.kurrent.io) containers.

## Getting Started

### Install the package

In your app host project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.KurrentDB
```

### Example usage

Then, in the _Program.cs_ file of app host, add a KurrentDB resource and consume the connection using the following methods:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var kurrentdb = builder.AddKurrentDB("kurrentdb");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(kurrentdb);

builder.Build().Run();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-kurrentdb

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

