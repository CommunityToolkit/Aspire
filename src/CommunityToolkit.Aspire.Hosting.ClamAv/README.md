# CommunityToolkit.Aspire.Hosting.ClamAv library

Provides extension methods and resource definitions for the .NET Aspire AppHost to support running [ClamAv Antivirus](https://www.clamav.net/) containers.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.ClamAv
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, add a ClamAv resource and consume the connection using the following methods:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var clamav = builder.ClamAv("antimalware");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(clamav);

builder.Build().Run();
```
