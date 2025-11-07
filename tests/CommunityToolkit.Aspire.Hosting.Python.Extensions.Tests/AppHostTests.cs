using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Python.Extensions.Tests;

#pragma warning disable CTASPIRE001
#pragma warning disable CS0612 // Type or member is obsolete
#pragma warning disable CS0618 // Type or member is obsolete
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Python_Extensions_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Python_Extensions_AppHost>>
{
    [Theory(Skip = "To be reviewed with https://github.com/CommunityToolkit/Aspire/issues/917")]
    [InlineData("uvicornapp")]
    [InlineData("uvapp")]
    public async Task ResourceStartsAndRespondsOk(string appName)
    {
        var httpClient = fixture.CreateHttpClient(appName);

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(appName).WaitAsync(TimeSpan.FromMinutes(5));

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
