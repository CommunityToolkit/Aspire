using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Stripe.Tests;

public class AddStripeTests
{
    [Fact]
    public void StripeUsesStripeCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddStripe("stripe");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<StripeResource>());

        Assert.Equal("stripe", resource.Command);
    }

    [Fact]
    public async Task StripeWithListenAddsListenArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddStripe("stripe")
            .WithListen("http://localhost:5082/webhooks");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<StripeResource>());

        var args = await resource.GetArgumentValuesAsync();

        Assert.Collection(args,
            arg => Assert.Equal("listen", arg),
            arg => Assert.Equal("--forward-to=http://localhost:5082/webhooks", arg)
        );
    }

    [Fact]
    public async Task StripeWithListenAndEventsAddsEventArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddStripe("stripe")
            .WithListen("http://localhost:5082/webhooks", events: "payment_intent.created,charge.succeeded");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<StripeResource>());

        var args = await resource.GetArgumentValuesAsync();

        Assert.Collection(args,
            arg => Assert.Equal("listen", arg),
            arg => Assert.Equal("--forward-to=http://localhost:5082/webhooks", arg),
            arg => Assert.Equal("--events", arg),
            arg => Assert.Equal("payment_intent.created,charge.succeeded", arg)
        );
    }

    [Fact]
    public async Task StripeWithApiKeyAddsApiKeyArg()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddStripe("stripe")
            .WithListen("http://localhost:5082/webhooks")
            .WithApiKey("sk_test_123");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<StripeResource>());

        var args = await resource.GetArgumentValuesAsync();

        Assert.Contains(args, arg => arg == "--api-key");
        Assert.Contains(args, arg => arg == "sk_test_123");
    }

    [Fact]
    public async Task StripeWithApiKeyParameterAddsApiKeyArg()
    {
        var builder = DistributedApplication.CreateBuilder();

        var apiKey = builder.AddParameter("stripe-api-key");

        builder.AddStripe("stripe")
            .WithListen("http://localhost:5082/webhooks")
            .WithApiKey(apiKey);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<StripeResource>());

        var config = await resource.GetEffectiveArgsAsync(default);

        Assert.Contains(config, arg => arg.Contains("--api-key="));
    }

    [Fact]
    public async Task StripeWithListenToEndpointReference()
    {
        var builder = DistributedApplication.CreateBuilder();

        var api = builder.AddProject<TestProject>("api")
            .WithHttpEndpoint(port: 5082, name: "http");

        builder.AddStripe("stripe")
            .WithListen(api.GetEndpoint("http"));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<StripeResource>());

        var config = await resource.GetEffectiveArgsAsync(default);

        Assert.Contains(config, arg => arg.Contains("--forward-to="));
    }

    [Fact]
    public void AddStripeNullBuilderThrows()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddStripe("stripe"));
    }

    [Fact]
    public void AddStripeNullNameThrows()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddStripe(null!));
    }

    [Fact]
    public void AddStripeEmptyNameThrows()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentException>(() => builder.AddStripe(""));
    }

    [Fact]
    public void WithListenNullBuilderThrows()
    {
        IResourceBuilder<StripeResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithListen("http://localhost:5000"));
    }

    [Fact]
    public void WithListenNullUrlThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var stripe = builder.AddStripe("stripe");

        Assert.Throws<ArgumentNullException>(() => stripe.WithListen((string)null!));
    }

    [Fact]
    public void WithListenEmptyUrlThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var stripe = builder.AddStripe("stripe");

        Assert.Throws<ArgumentException>(() => stripe.WithListen(""));
    }

    [Fact]
    public void WithListenNullEndpointReferenceThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var stripe = builder.AddStripe("stripe");

        Assert.Throws<ArgumentNullException>(() => stripe.WithListen((EndpointReference)null!));
    }

    [Fact]
    public void WithApiKeyNullBuilderThrows()
    {
        IResourceBuilder<StripeResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithApiKey("sk_test_123"));
    }

    [Fact]
    public void WithApiKeyNullKeyThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var stripe = builder.AddStripe("stripe");

        Assert.Throws<ArgumentNullException>(() => stripe.WithApiKey((string)null!));
    }

    [Fact]
    public void WithApiKeyEmptyKeyThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var stripe = builder.AddStripe("stripe");

        Assert.Throws<ArgumentException>(() => stripe.WithApiKey(""));
    }

    [Fact]
    public void WithApiKeyNullParameterThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var stripe = builder.AddStripe("stripe");

        Assert.Throws<ArgumentNullException>(() => stripe.WithApiKey((IResourceBuilder<ParameterResource>)null!));
    }

    [Fact]
    public void WithReferenceAddsWebhookSecretEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();

        var stripe = builder.AddStripe("stripe")
            .WithListen("http://localhost:5082/webhooks");

        var api = builder.AddProject<TestProject>("api")
            .WithReference(stripe);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var apiResource = Assert.Single(appModel.Resources.OfType<TestProject>());

        var envVars = apiResource.GetEnvironmentVariables();

        Assert.True(envVars.ContainsKey("STRIPE_WEBHOOK_SECRET"));
    }

    [Fact]
    public void WithReferenceCustomEnvironmentVariableName()
    {
        var builder = DistributedApplication.CreateBuilder();

        var stripe = builder.AddStripe("stripe")
            .WithListen("http://localhost:5082/webhooks");

        var api = builder.AddProject<TestProject>("api")
            .WithReference(stripe, webhookSigningSecretEnvVarName: "CUSTOM_STRIPE_SECRET");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var apiResource = Assert.Single(appModel.Resources.OfType<TestProject>());

        var envVars = apiResource.GetEnvironmentVariables();

        Assert.True(envVars.ContainsKey("CUSTOM_STRIPE_SECRET"));
        Assert.False(envVars.ContainsKey("STRIPE_WEBHOOK_SECRET"));
    }

    [Fact]
    public void WithReferenceNullBuilderThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var stripe = builder.AddStripe("stripe");

        IResourceBuilder<TestProject> apiBuilder = null!;

        Assert.Throws<ArgumentNullException>(() => apiBuilder.WithReference(stripe));
    }

    [Fact]
    public void WithReferenceNullSourceThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder.AddProject<TestProject>("api");

        Assert.Throws<ArgumentNullException>(() => api.WithReference((IResourceBuilder<StripeResource>)null!));
    }

    [Fact]
    public void WithReferenceNullEnvVarNameThrows()
    {
        var builder = DistributedApplication.CreateBuilder();

        var stripe = builder.AddStripe("stripe");
        var api = builder.AddProject<TestProject>("api");

        Assert.Throws<ArgumentNullException>(() => api.WithReference(stripe, webhookSigningSecretEnvVarName: null!));
    }

    [Fact]
    public void WithReferenceEmptyEnvVarNameThrows()
    {
        var builder = DistributedApplication.CreateBuilder();

        var stripe = builder.AddStripe("stripe");
        var api = builder.AddProject<TestProject>("api");

        Assert.Throws<ArgumentException>(() => api.WithReference(stripe, webhookSigningSecretEnvVarName: ""));
    }

    private class TestProject : ProjectResource, IResourceWithEnvironment
    {
        public TestProject(string name) : base(name)
        {
        }

        private readonly Dictionary<string, object> _env = [];

        public Dictionary<string, object> GetEnvironmentVariables() => _env;
    }
}
