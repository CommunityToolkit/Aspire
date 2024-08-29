using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace CommunityToolkit.Aspire.Testing;

public class AspireIntegrationTestFixture<T> : IAsyncLifetime
    where T : class
{
    private DistributedApplication app = null!;

    public DistributedApplication App => app;
    public ResourceNotificationService ResourceNotificationService => App.Services.GetRequiredService<ResourceNotificationService>();

    public async Task DisposeAsync() => await app.DisposeAsync();

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<T>();

        appHost.Services
            .AddLogging(builder =>
            {
                builder.AddXUnit();
                if (Environment.GetEnvironmentVariable("RUNNER_DEBUG") is not null or "1")
                    builder.SetMinimumLevel(LogLevel.Trace);
                else
                    builder.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler());

        app = await appHost.BuildAsync();
        await app.StartAsync();
    }
}