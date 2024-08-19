using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
namespace CommunityToolkit.Aspire.Testing;

public abstract class AspireIntegrationTest<T>(ITestOutputHelper testOutput) : IAsyncLifetime
    where T : class
{
    protected DistributedApplication app = null!;
    protected ResourceNotificationService ResourceNotificationService => app.Services.GetRequiredService<ResourceNotificationService>();

    public async Task DisposeAsync() => await app.DisposeAsync();

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<T>();

        appHost.Services
            .AddLogging(builder =>
            {
                builder.AddXUnit(testOutput);
                builder.SetMinimumLevel(LogLevel.Trace);
            })
            .ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        app = await appHost.BuildAsync();
        await app.StartAsync();
    }
}