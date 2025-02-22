# CommunityToolkit.Aspire.Hosting.SqlServer.Extensions library

This integration contains extensions for the [SqlServer hosting package](https://nuget.org/packages/Aspire.Hosting.SqlServer) for .NET Aspire.

The integration provides support for running [DbGate](https://github.com/dbgate/dbgate) to interact with the SqlServer database.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.SqlServer.Extensions
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define an SqlServer resource, then call `AddSqlServer`:

```csharp
var sqlserver = builder.AddSqlServer("sqlserver")
    .WithDbGate();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-sqlserver-extensions

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire