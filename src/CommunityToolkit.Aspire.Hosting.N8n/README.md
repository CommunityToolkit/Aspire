# CommunityToolkit.Aspire.Hosting.N8n library

An Aspire hosting integration for [n8n](https://n8n.io), a fair-code licensed workflow automation tool that combines AI capabilities with business process automation.

# Getting Started

## Install the package

In your AppHost project, install the package using the following command:```dotnetcli

dotnet add package CommunityToolkit.Aspire.Hosting.N8n

## Example usage

Then, in the \_Program.cs\_ file of `AppHost`, add a N8n resource using the following methods:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
var n8n = builder.AddN8n("n8n");
builder.Build().Run();
```

## Advanced Example usage

Szenario with postgres database, isolated worker with task runner using redis, additional paramters for instance owner and license key.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var ownerPassword = builder.AddParameter("owner-password", "Pass$Word1");
var licenseKey = builder.AddParameter("license-key", "[license-key]");

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var db = postgres.AddDatabase("n8n-db");

var redis = builder.AddRedis("redis")
    .WithDataVolume();

var n8n = builder.AddN8n("n8n", port: 5678)
    .WithDataBindMount("./.n8n_data")
    .WithPostgresDatabase(db)
    .WithQueueMode(redis)
    .WithInstanceOwner("admin@dev.local", "Admin", "Local", ownerPassword)
    .WithLicenseKey(licenseKey);

var worker = n8n.AddWorker("worker", port: 5679)
    .WithPostgresDatabase(db)
    .WithQueueMode(redis);

worker.AddTaskRunner("runner");

builder.Build().Run();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-n8n

# Feedback & contributing

https://github.com/CommunityToolkit/Aspire

