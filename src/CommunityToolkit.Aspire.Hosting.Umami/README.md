# CommunityToolkit.Aspire.Hosting.Umami library

Provides extension methods and resource definitions for the Aspire AppHost to support running [Umami](https://umami.is/) containers.

## Getting started

### Install the package

In your AppHost project, install the Aspire Umami Hosting library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Umami
```

## Usage example

Then, in the _Program.cs_ file of `AppHost`, add a Umami resource alongside a valid backend storage for Umami (only PostgreSQL is supported by Umami) using the following methods:

```csharp
var db = builder.AddPostgres("postgres")
                .AddDatabase("db");

var umami = builder.AddUmami("umami")
                   .WithStorageBackend(db);
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-umami

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
