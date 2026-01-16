using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Azure.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public async Task WithAzureStorageExplorerAddsAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();

        var azureStorageResourceBuilder = builder.AddAzureStorage("storage")
                .RunAsEmulator(azurite =>
                {
                    azurite
                        .WithBlobPort(27000)
                        .WithQueuePort(27001)
                        .WithTablePort(27002);
                });
        var blobsResourceBuilder = azureStorageResourceBuilder.AddBlobs("blobs").WithAzureStorageExplorer();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var azureStorageExplorerResource = appModel.Resources.OfType<AzureStorageExplorerResource>().SingleOrDefault();

        Assert.NotNull(azureStorageExplorerResource);

        Assert.Equal("blobs-explorer", azureStorageExplorerResource.Name);

#pragma warning disable CS0618 // Type or member is obsolete
        var envs = await azureStorageExplorerResource.GetEnvironmentVariableValuesAsync();
#pragma warning restore CS0618 // Type or member is obsolete

        Assert.NotEmpty(envs);

        Assert.Collection(envs,
            item =>
            {
                Assert.Equal("AZURE_STORAGE_CONNECTIONSTRING", item.Key);
                Assert.Equal("DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://storage.dev.internal:10000/devstoreaccount1;", item.Value);
            },
            item =>
            {
                Assert.Equal("AZURITE", item.Key);
                Assert.Equal("true", item.Value);
            });
    }

    [Fact]
    public void MultipleWithAzureStorageExplorerCallsAddsOneAzureStorageExplorerResourceForEachResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        var azureStorageResourceBuilder = builder.AddAzureStorage("storage")
            .RunAsEmulator(azurite =>
            {
                azurite
                    .WithBlobPort(27000)
                    .WithQueuePort(27001)
                    .WithTablePort(27002);
            });
        var blobsResourceBuilder = azureStorageResourceBuilder.AddBlobs("blobs").WithAzureStorageExplorer();
        var queuesResourceBuilder = azureStorageResourceBuilder.AddBlobs("queues").WithAzureStorageExplorer();
        var tablesResourceBuilder = azureStorageResourceBuilder.AddBlobs("tables").WithAzureStorageExplorer();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var azureStorageExplorerResources = appModel.Resources.OfType<AzureStorageExplorerResource>().ToList();
        
        Assert.Equal(3, azureStorageExplorerResources.Count);
        Assert.Equal("blobs-explorer", azureStorageExplorerResources[0].Name);
        Assert.Equal("queues-explorer", azureStorageExplorerResources[1].Name);
        Assert.Equal("tables-explorer", azureStorageExplorerResources[2].Name);
    }

    [Fact]
    public void WithAzureStorageExplorerShouldChangeAzureStorageExplorerHostPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        var azureStorageResourceBuilder = builder.AddAzureStorage("storage")
            .RunAsEmulator(azurite =>
            {
                azurite
                    .WithBlobPort(27000)
                    .WithQueuePort(27001)
                    .WithTablePort(27002);
            });
        var blobsResourceBuilder = azureStorageResourceBuilder
            .AddBlobs("blobs")
            .WithAzureStorageExplorer(c => c.WithHostPort(8068));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var azureStorageExplorerResource = appModel.Resources.OfType<AzureStorageExplorerResource>().SingleOrDefault();
        Assert.NotNull(azureStorageExplorerResource);

        var primaryEndpoint = azureStorageExplorerResource.Annotations.OfType<EndpointAnnotation>().Single();
        Assert.Equal(8068, primaryEndpoint.Port);
    }

    [Fact]
    public void WithAzureStorageExplorerShouldChangeAzureStorageExplorerContainerImageTag()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        var azureStorageResourceBuilder = builder.AddAzureStorage("storage")
            .RunAsEmulator(azurite =>
            {
                azurite
                    .WithBlobPort(27000)
                    .WithQueuePort(27001)
                    .WithTablePort(27002);
            });
        var blobsResourceBuilder = azureStorageResourceBuilder
            .AddBlobs("blobs")
            .WithAzureStorageExplorer(c => c.WithImageTag("manualTag"));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var azureStorageExplorerResource = appModel.Resources.OfType<AzureStorageExplorerResource>().SingleOrDefault();
        Assert.NotNull(azureStorageExplorerResource);

        var containerImageAnnotation = azureStorageExplorerResource.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("manualTag", containerImageAnnotation.Tag);
    }
}