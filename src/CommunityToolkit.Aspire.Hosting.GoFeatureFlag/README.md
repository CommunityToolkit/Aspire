# CommunityToolkit.Aspire.Hosting.GoFeatureFlag library

Provides extension methods and resource definitions for the .NET Aspire AppHost to support running [GO Feature Flag](https://gofeatureflag.org/) containers.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.GoFeatureFlag
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, add a GO Feature Flag resource and consume the connection using the following methods:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var goff = builder.AddGoFeatureFlag("goff");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(goff);

builder.Build().Run();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-go-feature-flag

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
