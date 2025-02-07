# CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions library

This integration contains extensions for the [MongoDB hosting package](https://nuget.org/packages/Aspire.Hosting.MongoDB) for .NET Aspire.

The integration provides support for running [DbGate](https://github.com/dbgate/dbgate) to interact with the MongoDB database.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.MongoDB.Extensions
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define an MongoDB resource, then call `AddMongoDB`:

```csharp
var mongodb = builder.AddMongoDB("mongodb")
    .WithDbGate();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-mongodb-extensions

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire