# CommunityToolkit.Aspire.Hosting.Redis.Extensions library

This integration contains extensions for the [Redis hosting package](https://nuget.org/packages/Aspire.Hosting.Redis) for .NET Aspire.

The integration provides support for running [DbGate](https://github.com/dbgate/dbgate) to interact with the Redis database.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Redis.Extensions
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define an Redis resource, then call `AddRedis`:

```csharp
var redis = builder.AddRedis("redis")
    .WithDbGate();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-redis-extensions

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire