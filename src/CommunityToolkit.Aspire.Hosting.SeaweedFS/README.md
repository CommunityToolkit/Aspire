# CommunityToolkit.Aspire.Hosting.SeaweedFS library

Provides extension methods and resource definitions for the Aspire AppHost to support running [SeaweedFS](https://github.com/seaweedfs/seaweedfs) containers with flexible API configurations (Native Master/Volume/Filer APIs or an S3-compatible Gateway).

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.SeaweedFS

```

### Example usage

Then, in the *Program.cs* file of `AppHost`, add a SeaweedFS resource, opt-in to the desired APIs, and consume the connection in your dependent projects:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Example 1: Standard usage enabling the S3-Compatible API Gateway
// Note: Enabling S3 implicitly enables and exposes the Filer API underneath.
var seaweedS3 = builder.AddSeaweedFS("seaweedfs")
                       .WithS3()
                       .WithDataVolume();

// Example 2: Native usage enabling only the Filer API (No S3 API running)
var seaweedNative = builder.AddSeaweedFS("seaweed-native")
                           .WithFiler()
                           .WithDataVolume();

// Reference the SeaweedFS resource in a project.
var apiService = builder.AddProject<Projects.ApiService>("apiservice")
                        .WithReference(seaweedS3);

builder.Build().Run();

```

When you use `.WithReference()`, the hosting library dynamically builds the connection string based on your opt-ins. For instance, if you chained `.WithS3()`, the connection string will inject the S3 endpoint, credentials, and the explicit `FilerEndpoint` to be consumed by the Aspire SeaweedFS Client package automatically.

## Configuring SeaweedFS

SeaweedFS provides several modular extension methods to configure its container based on your needs:

### API Activation (Opt-In)

* `WithS3(int? s3Port)`: Enables the SeaweedFS S3-Compatible API Gateway (defaults to target port `8333`). This implicitly enables the Filer API since it is required by the S3 gateway.
* `WithFiler(int? filerPort)`: Enables the SeaweedFS Native Filer API (defaults to target port `8888`) without starting the S3 gateway.
* `WithHostPort(int? port)`: Configures the explicit host port for the core Master API (defaults to target port `9333`).

### Credentials & Security

* `WithAccessKey(IResourceBuilder<ParameterResource> accessKey)`: Configures a custom Access Key parameter for the S3 identity mapping wrapper.
* `WithSecretKey(IResourceBuilder<ParameterResource> secretKey)`: Configures a custom Secret Key parameter for the S3 identity mapping wrapper.
* `WithS3ConfigFile(string configFilePath)`: Overrides the dynamic configuration injection and enforces using a specific custom local `s3.json` configuration file mounted directly into the container.

### Persistence

* `WithDataBindMount(string source, bool isReadOnly)`: Binds a local host directory to the container's `/data` folder to persist your cluster data across restarts.
* `WithDataVolume(string? name, bool isReadOnly)`: Uses a named Docker volume to persist SeaweedFS cluster data.

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire