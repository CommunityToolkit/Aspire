# CommunityToolkit.Aspire.Hosting.RavenDB library

Provides extension methods and resource definitions for an Aspire AppHost to configure a RavenDB resource.

## Getting started

### Install the package

In your AppHost project, install the Aspire RavenDB Hosting library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.RavenDB
```

## Usage example

Then, in the _Program.cs_ file of `AppHost`, add a RavenDB resource and consume the connection using the following methods:

```csharp
var db = builder.AddRavenDB("ravendb").AddDatabase("mydb");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(db);
```

## Additional documentation

<!-- TODO: Update the link once it is created -->
https://learn.microsoft.com/dotnet/aspire/community-toolkit/ravendb

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
