# CommunityToolkit.Aspire.Hosting.MySql.Extensions library

This integration contains extensions for the [MySql hosting package](https://nuget.org/packages/Aspire.Hosting.MySql) for .NET Aspire.

The integration provides support for running [Adminer](https://github.com/vrana/adminer) and [DbGate](https://github.com/dbgate/dbgate) to interact with the MySql database.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.MySql.Extensions
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define an MySql resource, then call `AddMySql`:

```csharp
var mysql = builder.AddMySql("mysql")
    .WithDbGate()
    .WithAdminer();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-mysql-extensions

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire