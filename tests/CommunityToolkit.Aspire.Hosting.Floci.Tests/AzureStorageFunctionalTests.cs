using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

[RequiresDocker]
public class AzureStorageFunctionalTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task StorageBecomesHealthyAndSupportsBlobSdkRoundTrip()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var cancellationToken = timeout.Token;

        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);
        var azure = builder.AddFlociAzure("floci-az");
        var storage = azure.WithStorage();

        await using var app = await builder.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync(storage.Resource.Name, cancellationToken);

        string? connectionString = await app.GetConnectionStringAsync(storage.Resource.Name, cancellationToken);
        Assert.NotNull(connectionString);

        var service = new BlobServiceClient(connectionString);
        Assert.Equal(storage.Resource.AccountName, service.AccountName);

        var container = service.GetBlobContainerClient($"test-{Guid.NewGuid():N}");
        await container.CreateAsync(cancellationToken: cancellationToken);
        var blob = container.GetBlobClient("greeting.txt");
        await blob.UploadAsync(BinaryData.FromString("hello"), cancellationToken);

        var download = await blob.DownloadContentAsync(cancellationToken);
        Assert.Equal("hello", download.Value.Content.ToString());

        // SAS generation needs the account name from the URL path, which only works for IP hosts.
        Assert.True(blob.CanGenerateSasUri);
        var sasUri = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.StartsWith($"{service.Uri.Scheme}://127.0.0.1:", sasUri.ToString());
        var sasDownload = await new BlobClient(sasUri).DownloadContentAsync(cancellationToken);
        Assert.Equal("hello", sasDownload.Value.Content.ToString());

        await container.DeleteAsync(cancellationToken: cancellationToken);
    }
}
