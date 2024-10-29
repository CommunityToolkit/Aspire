using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using FluentAssertions;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SqlDatabaseProjects_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SqlDatabaseProjects_AppHost>>
{
    [Theory]
    [InlineData("sdk-project")]
    public async Task ResourceStartsAndRespondsOk(string resourceName)
    {
        await fixture.ResourceNotificationService.WaitForResourceAsync(resourceName, KnownResourceStates.Finished).WaitAsync(TimeSpan.FromMinutes(5));
    }
}