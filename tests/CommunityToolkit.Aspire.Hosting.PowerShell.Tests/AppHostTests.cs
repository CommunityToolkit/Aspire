using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.PowerShell.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_PowerShell_AppHost> fixture) :
    IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_PowerShell_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var resourceName = "ps";
        await fixture.ResourceNotificationService.WaitForResourceAsync(resourceName, KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    //[Fact]
    //public async Task OllamaListsAvailableModels()
    //{
    //    var distributedAppModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
    //    var modelResources = distributedAppModel.Resources.OfType<OllamaModelResource>().ToList();
    //    var rns = fixture.ResourceNotificationService;

    //    await Task.WhenAll([
    //        rns.WaitForResourceHealthyAsync("ollama"),
    //        .. modelResources.Select(m => rns.WaitForResourceHealthyAsync(m.Name))
    //    ]).WaitAsync(TimeSpan.FromMinutes(10));
    //    var httpClient = fixture.CreateHttpClient("ollama");

    //    var models = (await new OllamaApiClient(httpClient).ListLocalModelsAsync()).ToList();

    //    Assert.NotEmpty(models);
    //    Assert.Equal(modelResources.Count, models.Count);
    //}
}

public class AppHostTestsX
{
    private readonly ITestOutputHelper _testOutputHelper;

    public AppHostTestsX(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(90);


    //[Fact]
    //public async Task EnsureScriptExecutes()
    //{
    //    var cancellationToken = new CancellationTokenSource(DefaultTimeout).Token;
    //    var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AspirePowerShell_AppHost>(cancellationToken);

    //    appHost.Services.AddLogging(logging =>
    //    {
    //        logging.SetMinimumLevel(LogLevel.Debug);
            
    //        // Override the logging filters from the app's configuration
    //        logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
    //        logging.AddFilter("Aspire.", LogLevel.Debug);
            
    //        logging.AddXUnit(_testOutputHelper);
    //    });

    //    await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

    //    await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

    //    await foreach (var notification in  app.ResourceNotifications.WatchAsync(cancellationToken))
    //    {
    //        _testOutputHelper.WriteLine($"Notification: {notification.Resource.Name} - {notification.Snapshot.State} - {notification.Snapshot.HealthStatus}");
    //    }

    //    Assert.True(true);
    //}
}
