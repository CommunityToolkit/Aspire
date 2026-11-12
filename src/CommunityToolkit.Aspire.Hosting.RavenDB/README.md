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

### Open databases in RavenDB Studio

Every database added with `AddDatabase(...)` exposes a clickable **RavenDB Studio** link in the Aspire dashboard that opens that database's Documents view directly (for example `http://localhost:9534/studio/index.html#databases/documents?&database=mydb`). The link is added automatically — no extra configuration is required. For secured servers it uses the configured public server URL.

### Deploy to RavenDB Cloud

`aspire run` starts RavenDB in a local container. To deploy the applications against a RavenDB Cloud product, add the [CommunityToolkit.Aspire.Hosting.RavenDB.Cloud](https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.RavenDB.Cloud) package and call `PublishAsRavenDBCloud(...)` on the server: `aspire deploy` then creates or finds the product and gives every application the product URL and a client certificate of its own.

## Additional documentation

<!-- TODO: Update the link once it is created -->
https://learn.microsoft.com/dotnet/aspire/community-toolkit/ravendb

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
