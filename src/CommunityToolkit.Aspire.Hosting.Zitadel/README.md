# CommunityToolkit.Aspire.Hosting.Zitadel library

Provides extension methods and resource definitions for a .NET Aspire AppHost to configure Zitadel.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Zitadel
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Zitadel resource, then call `AddZitadel`:

```csharp
builder.AddZitadel("zitadel");
```

Zitadel *requires* a Postgres database, you can add one with `AddDatabase`:
```csharp
var database = builder.AddPostgres("postgres");

builder.AddZitadel("zitadel")
    .AddDatabase(database);
```
You can also pass in a database rather than server (`AddPostgres().AddDatabase()`).

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
