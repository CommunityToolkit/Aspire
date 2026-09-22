var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("azure-storage")
    .RunAsEmulator(azurite =>
    {
        azurite
            .WithArgs("--disableProductStyleUrl")
            .WithBlobPort(27000)
            .WithQueuePort(27001)
            .WithTablePort(27002)
            .WithDataVolume("storage");
    });

var blobs = storage.AddBlobs("blobs").WithAzureStorageExplorer();

builder.Build().Run();