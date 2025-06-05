# CommunityToolkit.Aspire.Hosting.MinIO library

Provides extension methods and resource definitions for the .NET Aspire AppHost to support running [MinIO](https://min.io/) containers.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Minio
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, add a MinIO resource and consume the connection using the following methods:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var minio = builder.AddMinio("minio");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(minio);

builder.Build().Run();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-minio

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

