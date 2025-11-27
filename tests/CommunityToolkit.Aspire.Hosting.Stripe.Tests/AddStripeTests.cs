using Aspire.Hosting;

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

        var externalEndpoint = builder.AddExternalService("external-api", "http://localhost:5082");

        builder.AddStripe("stripe")
            .WithListen(externalEndpoint, webhookPath: "webhooks");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<StripeResource>());

        var args = await resource.GetArgumentValuesAsync();

        Assert.Collection(args,
            arg => Assert.Equal("listen", arg),
            arg => Assert.Equal("--forward-to", arg),
            arg => Assert.Equal("http://localhost:5082/webhooks", arg)
        );
    }

    [Fact]
    public async Task StripeWithListenAndEventsAddsEventArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        var externalEndpoint = builder.AddExternalService("external-api", "http://localhost:5082");

        builder.AddStripe("stripe")
            .WithListen(externalEndpoint, webhookPath: "webhooks", events: ["payment_intent.created,charge.succeeded"]);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<StripeResource>());

        var args = await resource.GetArgumentValuesAsync();

        Assert.Collection(args,
            arg => Assert.Equal("listen", arg),
            arg => Assert.Equal("--forward-to", arg),
            arg => Assert.Equal("http://localhost:5082/webhooks", arg),
            arg => Assert.Equal("--events", arg),
            arg => Assert.Equal("payment_intent.created,charge.succeeded", arg)
        );
    }

    [Fact]
    public async Task StripeWithApiKeyAddsApiKeyArg()
    {
        var builder = DistributedApplication.CreateBuilder();

        var externalEndpoint = builder.AddExternalService("external-api", "http://localhost:5082");
        var apiKey = builder.AddParameter("api-key", "sk_test_123");

        builder.AddStripe("stripe")
            .WithListen(externalEndpoint)
            .WithApiKey(apiKey);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<StripeResource>());

        var args = await resource.GetArgumentValuesAsync();

        Assert.Contains(args, arg => arg == "--api-key");
        Assert.Contains(args, arg => arg == "sk_test_123");
    }

    [Fact]
    public void StripeWithApiKeyParameterAddsApiKeyArg()
    {
        var builder = DistributedApplication.CreateBuilder();

        var apiKey = builder.AddParameter("stripe-api-key");

        var externalEndpoint = builder.AddExternalService("external-api", "http://localhost:5082");

        builder.AddStripe("stripe")
            .WithListen(externalEndpoint)
            .WithApiKey(apiKey);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<StripeResource>());

        // Verify that a CommandLineArgsCallbackAnnotation was added
        var argsAnnotation = resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>();
        Assert.NotEmpty(argsAnnotation);
    }

    [Fact]
    public void StripeWithListenToEndpointReference()
    {
        var builder = DistributedApplication.CreateBuilder();

        var api = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Stripe_Api>("api");

        var stripe = builder.AddStripe("stripe")
            .WithListen(api);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<StripeResource>());

        // Verify that a CommandLineArgsCallbackAnnotation was added
        var argsAnnotation = resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>();
        Assert.NotEmpty(argsAnnotation);
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

        Assert.Throws<ArgumentException>(() => builder.AddStripe(""!));
    }

    [Fact]
    public void WithListenNullBuilderThrows()
    {
        IResourceBuilder<StripeResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithListen((IResourceBuilder<IResourceWithEndpoints>)null!));
    }

    [Fact]
    public void WithListenNullUrlThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var stripe = builder.AddStripe("stripe");

        Assert.Throws<ArgumentNullException>(() => stripe.WithListen((IResourceBuilder<ExternalServiceResource>)null!));
    }

    [Fact]
    public void WithListenNullEndpointReferenceThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var stripe = builder.AddStripe("stripe");

        Assert.Throws<ArgumentNullException>(() => stripe.WithListen((IResourceBuilder<IResourceWithEndpoints>)null!));
    }

    [Fact]
    public void WithApiKeyNullBuilderThrows()
    {
        IResourceBuilder<StripeResource> builder = null!;

        var ex = Record.Exception(() => builder.WithApiKey(null!));

        var aex = Assert.IsType<ArgumentNullException>(ex);
        Assert.Equal("builder", aex.ParamName);
    }

    [Fact]
    public void WithApiKeyNullKeyThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var stripe = builder.AddStripe("stripe");

        var ex = Record.Exception(() => stripe.WithApiKey(null!));

        var aex = Assert.IsType<ArgumentNullException>(ex);
        Assert.Equal("apiKey", aex.ParamName);
    }

    [Fact]
    public void WithReferenceAddsWebhookSecretEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();

        var api = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Stripe_Api>("api");

        var stripe = builder.AddStripe("stripe")
            .WithListen(api);

        api.WithReference(stripe);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var apiResource = Assert.Single(appModel.Resources.OfType<ProjectResource>());

        // Verify that an EnvironmentCallbackAnnotation was added
        var envAnnotations = apiResource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void WithReferenceCustomEnvironmentVariableName()
    {
        var builder = DistributedApplication.CreateBuilder();

        var api = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Stripe_Api>("api");
        var stripe = builder.AddStripe("stripe")
            .WithListen(api);

        api.WithReference(stripe, webhookSigningSecretEnvVarName: "CUSTOM_STRIPE_SECRET");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var apiResource = Assert.Single(appModel.Resources.OfType<ProjectResource>());

        // Verify that an EnvironmentCallbackAnnotation was added
        var envAnnotations = apiResource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void WithReferenceNullBuilderThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var stripe = builder.AddStripe("stripe");

        IResourceBuilder<ProjectResource> apiBuilder = null!;

        Assert.Throws<ArgumentNullException>(() => apiBuilder.WithReference(stripe));
    }

    [Fact]
    public void WithReferenceNullSourceThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Stripe_Api>("api");

        Assert.Throws<ArgumentNullException>(() => api.WithReference((IResourceBuilder<StripeResource>)null!));
    }

    [Fact]
    public void WithReferenceNullEnvVarNameThrows()
    {
        var builder = DistributedApplication.CreateBuilder();

        var stripe = builder.AddStripe("stripe");
        var api = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Stripe_Api>("api");

        Assert.Throws<ArgumentNullException>(() => api.WithReference(stripe, webhookSigningSecretEnvVarName: null!));
    }

    [Fact]
    public void WithReferenceEmptyEnvVarNameThrows()
    {
        var builder = DistributedApplication.CreateBuilder();

        var stripe = builder.AddStripe("stripe");
        var api = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Stripe_Api>("api");

        Assert.Throws<ArgumentException>(() => api.WithReference(stripe, webhookSigningSecretEnvVarName: ""));
    }
}
