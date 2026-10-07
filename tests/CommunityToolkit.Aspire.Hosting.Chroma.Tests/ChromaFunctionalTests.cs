using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using ChromaDB.Client;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Chroma.Tests;

[RequiresDocker]
public class ChromaFunctionalTests(ITestOutputHelper testOutputHelper)
{
    private const string CollectionName = "persisted";

    // Chroma 0.x keeps its data in another folder than Chroma 1.x, the default image.
    [Theory]
    [InlineData(true, null)]
    [InlineData(false, null)]
    [InlineData(true, "0.6.3")]
    [InlineData(false, "0.6.3")]
    public async Task WithDataShouldPersistStateBetweenUsages(bool useVolume, string? imageTag)
    {
        string? volumeName = null;
        string? bindMountPath = null;

        try
        {
            using var builder1 = TestDistributedApplicationBuilder.Create(testOutputHelper);
            var chroma1 = builder1.AddChroma("chroma");
            if (imageTag is not null)
            {
                chroma1.WithImageTag(imageTag);
            }

            if (useVolume)
            {
                // Use a deterministic volume name to prevent them from exhausting the machines if deletion fails
                volumeName = VolumeNameGenerator.Generate(chroma1, $"{nameof(WithDataShouldPersistStateBetweenUsages)}{imageTag}");

                // if the volume already exists (because of a crashing previous run), delete it
                DockerUtils.AttemptDeleteDockerVolume(volumeName, throwOnFailure: true);
                chroma1.WithDataVolume(volumeName);
            }
            else
            {
                bindMountPath = Directory.CreateTempSubdirectory().FullName;
                chroma1.WithDataBindMount(bindMountPath);
            }

            using (var app = builder1.Build())
            {
                await app.StartAsync();

                var rns = app.Services.GetRequiredService<ResourceNotificationService>();
                await rns.WaitForResourceHealthyAsync(chroma1.Resource.Name);

                try
                {
                    using var host = await StartClientHostAsync(chroma1.Resource);
                    var chromaClient = host.Services.GetRequiredService<ChromaClient>();
                    var collection = await chromaClient.CreateCollectionAsync(CollectionName);
                    await chromaClient.GetCollectionClient(collection).AddAsync(["1"], embeddings: [new([0.1f, 0.2f, 0.3f])], documents: ["kept"]);
                }
                finally
                {
                    // Stops the container, or the Volume would still be in use
                    await app.StopAsync();
                }
            }

            using var builder2 = TestDistributedApplicationBuilder.Create(testOutputHelper);
            var chroma2 = builder2.AddChroma("chroma");
            if (imageTag is not null)
            {
                chroma2.WithImageTag(imageTag);
            }

            if (useVolume)
            {
                chroma2.WithDataVolume(volumeName);
            }
            else
            {
                chroma2.WithDataBindMount(bindMountPath!);
            }

            using (var app = builder2.Build())
            {
                await app.StartAsync();

                var rns = app.Services.GetRequiredService<ResourceNotificationService>();
                await rns.WaitForResourceHealthyAsync(chroma2.Resource.Name);

                try
                {
                    using var host = await StartClientHostAsync(chroma2.Resource);
                    var chromaClient = host.Services.GetRequiredService<ChromaClient>();
                    var collectionClient = chromaClient.GetCollectionClient(await chromaClient.GetCollectionAsync(CollectionName));
                    var entry = await collectionClient.GetAsync("1");
                    Assert.NotNull(entry);
                    Assert.Equal("kept", entry.Document);
                }
                finally
                {
                    // Stops the container, or the Volume would still be in use
                    await app.StopAsync();
                }
            }
        }
        finally
        {
            if (volumeName is not null)
            {
                DockerUtils.AttemptDeleteDockerVolume(volumeName);
            }

            if (bindMountPath is not null)
            {
                try
                {
                    Directory.Delete(bindMountPath, recursive: true);
                }
                catch
                {
                    // Don't fail test if we can't clean the temporary folder
                }
            }
        }
    }

    private static async Task<IHost> StartClientHostAsync(ChromaResource resource)
    {
        var hb = Host.CreateApplicationBuilder();
        hb.Configuration[$"ConnectionStrings:{resource.Name}"] = await resource.ConnectionStringExpression.GetValueAsync(default);
        hb.AddChromaClient(resource.Name);

        var host = hb.Build();
        await host.StartAsync();
        return host;
    }
}
