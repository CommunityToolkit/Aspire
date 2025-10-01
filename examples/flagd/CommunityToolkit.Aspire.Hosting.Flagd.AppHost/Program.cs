using OpenFeature.Contrib.Providers.Flagd;

var builder = DistributedApplication.CreateBuilder(args);

// Add flagd with local flag configuration file
var flagd = builder
    .AddFlagd("flagd", 8013)
    .WithBindFileSync("./flags/", "flagd.json")
    .WithLogging();

builder.Eventing.Subscribe<AfterResourcesCreatedEvent>(static async (@event, cancellationToken) =>
    {
        // When the resources are created, set the OpenFeature provider to use Flagd (debug purposes)
        await OpenFeature.Api.Instance.SetProviderAsync(new FlagdProvider());

        var flagClient = OpenFeature.Api.Instance.GetClient();
        var welcomeBanner = await flagClient.GetBooleanDetailsAsync("welcome-banner", false);
        var backgroundColor = await flagClient.GetStringDetailsAsync("background-color", "000000");
        var apiVersion = await flagClient.GetStringDetailsAsync("api-version", "0.1");
    });

builder.Build().Run();
