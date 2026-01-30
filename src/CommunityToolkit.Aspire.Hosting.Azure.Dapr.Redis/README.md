# CommunityToolkit.Aspire.Hosting.Azure.Dapr.Redis library

This package provides [Aspire](https://aspire.dev/get-started/what-is-aspire/) integration for Azure Redis as a Dapr component. It allows you to configure dapr state to use Azure Redis as part of your Aspire AppHost projects. 

## Usage
To use this package, install it into your Aspire AppHost project:

```bash
dotnet add package CommunityToolkit.Aspire.Hosting.Azure.Dapr.Redis
```

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var redisState = builder.AddAzureRedis("redisState")
                        .RunAsContainer(); // for local development

var daprState = builder.AddDaprStateStore("daprState")
                       .WithReference(redisState); //instructs aspire to use azure redis when publishing

var api = builder.AddProject<Projects.MyApiService>("example-api")
    .WithReference(daprState)
    .WithDaprSidecar();

builder.Build().Run();

```

## Notes

The current version of the integration currently focuses on publishing and does not make any changes to how dapr components are handled in local development