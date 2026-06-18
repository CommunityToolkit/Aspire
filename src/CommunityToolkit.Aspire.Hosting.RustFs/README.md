# CommunityToolkit.Aspire.Hosting.RustFs library

Provides extension methods and resource definitions for the Aspire AppHost to support running [RustFs](https://github.com/rustfs/rustfs) containers.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.RustFs
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, add a RustFs resource and consume the connection using the following methods:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var rustfs = builder.AddRustFs("rustfs")
    .WithDataVolume()
    .AddBucket("mybucket");

builder.Build().Run();
```

### Buckets

`AddBucket` registers each bucket as a child `RustFsBucketResource` so it appears in
the Aspire Dashboard. After the RustFs server becomes healthy, the integration issues
a signed S3 `PUT /{bucket}` request from the AppHost process to provision the bucket;
there is no `minio/mc` sidecar container.

Multiple buckets can be added at once via `AddBucket(IReadOnlyList<string>)`:

```csharp
builder.AddRustFs("rustfs")
    .AddBucket(["images", "documents", "audit-log"]);
```

