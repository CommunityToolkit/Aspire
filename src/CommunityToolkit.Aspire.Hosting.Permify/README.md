# CommunityToolkit.Aspire.Hosting.Permify library

Provides extension methods and resource definitions for the .NET Aspire AppHost to support running [Permify](https://permify.co/) containers
with optional watch support.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Permify
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Permify resource, then call `AddPermify`:

```csharp
var permify = builder.AddPermify("permify");
```

If you want to enable watch support you'll need a Postgres database:

```csharp
var postgres = builder.AddPostgres("postgres")
    .AddDatabase("permify-db");

var permify = builder.AddPermify("permify")
    .WithWatchSupport(postgres);
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/ollama

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

