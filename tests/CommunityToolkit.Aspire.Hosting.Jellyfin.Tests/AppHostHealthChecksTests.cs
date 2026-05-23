using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;

namespace CommunityToolkit.Aspire.Hosting.Jellyfin.Tests;

[RequiresDocker]
public sealed class AppHostHealthChecksTests(AppHostHealthChecksTests.JellyfinAspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Jellyfin_AppHost> fixture)
    : IClassFixture<AppHostHealthChecksTests.JellyfinAspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Jellyfin_AppHost>>
{
    private const string ResourceName = "jellyfin";

    [Fact]
    public async Task ResourceStartsHealthyUsingCustomHttpPort()
    {
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(ResourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));
    }

    public sealed class JellyfinAspireIntegrationTestFixture<TEntryPoint>()
        : AspireIntegrationTestFixture<TEntryPoint> where TEntryPoint : class
    {
        protected override void OnBuilderCreated(DistributedApplicationBuilder applicationBuilder)
        {
            base.OnBuilderCreated(applicationBuilder);
            applicationBuilder.Configuration.AddInMemoryCollection([
                new KeyValuePair<string, string?>("Jellyfin:HttpPort", $"{GetRandomPort()}"),
            ]);
        }

        private static int GetRandomPort() => RandomNumberGenerator.GetInt32(1024, 65535);
    }
}
