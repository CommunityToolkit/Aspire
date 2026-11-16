using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Keycloak.Extensions.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Keycloak.Extensions.AppHost.Go",
            exampleName: "keycloak-postgres",
            // The preview Go SDK cannot serialize optional PostgreSQL credential parameter handles.
            validatePublish: false,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
