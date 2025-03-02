using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;

namespace CommunityToolkit.Aspire.Hosting.MailPit.Tests;

[RequiresDocker]
public sealed class AppHostHealthChecksTests(AppHostHealthChecksTests.MailPitAspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_MailPit_AppHost> fixture)
    : IClassFixture<AppHostHealthChecksTests.MailPitAspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_MailPit_AppHost>>
{
    private const string ResourceName = "mailpit";

    [Fact]
    public async Task ResourceStartsHealthyUsingCustomHttpPort()
    {
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(ResourceName)
            .WaitAsync(TimeSpan.FromMinutes(1));
    }

    public sealed class MailPitAspireIntegrationTestFixture<TEntryPoint>()
        : AspireIntegrationTestFixture<TEntryPoint> where TEntryPoint : class
    {
        protected override void OnBuilderCreated(DistributedApplicationBuilder applicationBuilder)
        {
            base.OnBuilderCreated(applicationBuilder);
            applicationBuilder.Configuration.AddInMemoryCollection([
                new KeyValuePair<string, string?>("MailPit:HttpPort", $"{GetRandomPort()}"),
            ]);
        }

        private static int GetRandomPort()
        {
            return RandomNumberGenerator.GetInt32(1024, 65535);
        }
    }
}