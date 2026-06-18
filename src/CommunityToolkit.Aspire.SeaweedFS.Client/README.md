# CommunityToolkit.Aspire.SeaweedFS.Client library

Provides an Aspire client integration for SeaweedFS. It supports both the S3-compatible API using the standard `AWSSDK.S3` client, and the Native Filer API through a strongly-typed `HttpClient`.

## Usage example

In the *Program.cs* file of your project, you can register the SeaweedFS clients depending on the APIs you enabled in your AppHost. 

### Registering the S3 Client
Call the `AddSeaweedFSS3Client` extension method to register an `IAmazonS3` client for use via the dependency injection container.

```csharp
builder.AddSeaweedFSS3Client("seaweedfs");

```

### Registering the Native Filer Client

Call the `AddSeaweedFSFilerClient` extension method to register a `SeaweedFSFilerClient` (which wraps a configured `HttpClient` pointing directly to the Filer node).

```csharp
builder.AddSeaweedFSFilerClient("seaweedfs");

```

## Configuration

The Aspire SeaweedFS Client integration provides multiple options to configure the server connection based on the requirements and conventions of your project.

### Use a connection string

When using a connection string from the `ConnectionStrings` configuration section, you can provide the name of the connection string when calling the builder methods. The connection string dynamically handles multiple endpoints (Master, S3, Filer) injected by the AppHost.

```json
{
    "ConnectionStrings": {
        "seaweedfs": "Endpoint=http://localhost:8333;FilerEndpoint=http://localhost:8888;AccessKey=admin;SecretKey=admin-secret;UseSsl=false"
    }
}

```

### Use configuration providers

The client supports [Microsoft.Extensions.Configuration](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration). It loads the `SeaweedFSClientSettings` from configuration by using the `Aspire:SeaweedFS:Client` key.

Example `appsettings.json` that configures both endpoints and enables SSL:

```json
{
  "Aspire": {
    "SeaweedFS": {
      "Client": {
        "Endpoint": "http://localhost:8333",
        "FilerEndpoint": "http://localhost:8888",
        "AccessKey": "admin",
        "SecretKey": "admin-secret",
        "ForcePathStyle": true,
        "UseSsl": false,
        "DisableHealthChecks": false
      }
    }
  }
}

```

> **Note:** `ForcePathStyle` defaults to `true` as it is strictly required by the SeaweedFS architecture to correctly route S3 bucket requests.

### Use inline delegates

You can also pass the `Action<SeaweedFSClientSettings> configureSettings` delegate to set up some or all the options inline. This is particularly useful for applying advanced S3 configurations:

```csharp
builder.AddSeaweedFSS3Client("seaweedfs", configureSettings: settings => 
{
    settings.AccessKey = "admin";
    settings.SecretKey = "admin-secret";
    settings.ConfigureS3Config = s3Config => 
    {
        s3Config.Timeout = TimeSpan.FromSeconds(30);
        s3Config.MaxErrorRetry = 3;
    };
});

```

## Consuming the Clients

Once registered, you can inject the clients into your services.

**Using the S3 API:**

```csharp
using Amazon.S3;
using Amazon.S3.Model;

public class StorageService(IAmazonS3 s3Client)
{
    public async Task CreateBucketAsync(string bucketName)
    {
        await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });
    }
}

```

**Using the Native Filer API:**

```csharp
using CommunityToolkit.Aspire.SeaweedFS.Client;

public class NativeFileService(SeaweedFSFilerClient filerClient)
{
    public async Task<string> ListFilesAsync()
    {
        // Requires Accept header for JSON responses from Filer
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Accept", "application/json");
        
        var response = await filerClient.HttpClient.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }
}

```

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire