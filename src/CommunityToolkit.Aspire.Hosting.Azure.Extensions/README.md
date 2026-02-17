# CommunityToolkit.Aspire.Hosting.Azure.Extensions library

This integration contains extensions for the [Azure Storage hosting package](https://nuget.org/packages/Aspire.Hosting.Azure.Storage) for Aspire.

The integration provides support for running [Azure Storage Explorer](https://github.com/sebagomez/azurestorageexplorer) to interact with the Azure Storage resource: blobs, queues and tables.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Azure.Extensions
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define an Azure Storage resource, then call `AddAzureStorageExplorer`:

```csharp
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite =>
    {
        azurite
            .WithBlobPort(27000)
            .WithQueuePort(27001)
            .WithTablePort(27002);
    });
var blobs = storage.AddBlobs("blobs").WithAzureStorageExplorer();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-azure-extensions

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire