using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.PowerShell.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_PowerShell_AppHost> fixture) :
    IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_PowerShell_AppHost>>
{
    [Fact]
    public async Task PowerShellResourceStarts()
    {
        var resourceName = "ps";
        await fixture.ResourceNotificationService
            .WaitForResourceAsync(resourceName, KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromSeconds(60));

        Assert.True(true);
    }

    [Fact]
    public async Task ScriptsExecuteSuccessfully()
    {
        var model = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();

        var script1 = model.Resources
            .OfType<PowerShellScriptResource>()
            .Single(r => r.Name == "script1");

        var ready1 = fixture.ResourceNotificationService
            .WaitForResourceAsync(script1.Name, KnownResourceStates.Finished)
            .WaitAsync(TimeSpan.FromSeconds(90));

        var script2 = model.Resources
            .OfType<PowerShellScriptResource>()
            .Single(r => r.Name == "script2");

        var ready2 = fixture.ResourceNotificationService
            .WaitForResourceAsync(script2.Name, KnownResourceStates.Finished)
            .WaitAsync(TimeSpan.FromSeconds(90));

        await Task.WhenAll([ready1, ready2]);

        Assert.True(ready1.IsCompletedSuccessfully);
        Assert.True(ready2.IsCompletedSuccessfully);
    }
}

