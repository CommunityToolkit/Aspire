using Aspire.Hosting;
using Aspire.Hosting.Eventing;
using Microsoft.Extensions.Options;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

public class TlsTests
{
    [Fact]
    public void EmulatorsAreHttpByDefault()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociAws("floci");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<FlociAwsContainerResource>().Single();

        Assert.True(resource.TryGetLastAnnotation(out EndpointAnnotation? endpoint));
        Assert.Equal("http", endpoint.UriScheme);
    }

    [Fact]
    public async Task AnAspireCertificateSwitchesThePrimaryEndpointToHttps()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociAws("floci").WithHttpsDeveloperCertificate();

        using var app = builder.Build();
        await PublishBeforeStartAsync(builder, app);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<FlociAwsContainerResource>().Single();

        Assert.True(resource.TryGetLastAnnotation(out EndpointAnnotation? endpoint));
        Assert.Equal("https", endpoint.UriScheme);
    }

    [Fact]
    public async Task AnAspireCertificateSwitchesTheAzurePrimaryEndpointToHttps()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociAzure("floci-az").WithHttpsDeveloperCertificate();

        using var app = builder.Build();
        await PublishBeforeStartAsync(builder, app);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<FlociAzureContainerResource>().Single();

        Assert.True(resource.TryGetLastAnnotation(out EndpointAnnotation? endpoint));
        Assert.Equal("https", endpoint.UriScheme);
    }

    [Fact]
    public async Task WithoutACertificateThePrimaryEndpointStaysHttp()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // A trusted development certificate on the machine must not flip an emulator to https on
        // its own — plain HTTP stays the default until a certificate is asked for.
        builder.AddFlociAws("floci");

        using var app = builder.Build();
        await PublishBeforeStartAsync(builder, app);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<FlociAwsContainerResource>().Single();

        Assert.True(resource.TryGetLastAnnotation(out EndpointAnnotation? endpoint));
        Assert.Equal("http", endpoint.UriScheme);
    }

    [Theory]
    [InlineData("floci", "FLOCI_TLS_ENABLED", "FLOCI_TLS_SELF_SIGNED", "FLOCI_TLS_CERT_PATH", "FLOCI_TLS_KEY_PATH")]
    public async Task AnAspireProvidedCertificateIsMappedOntoTheImagesTlsEnvironmentVariables(
        string name, string enabled, string selfSigned, string certPath, string keyPath)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var floci = builder.AddFlociAws(name);

        // AddFlociAws registers the callback; Aspire invokes it once it has provisioned a key pair
        // for the resource (WithHttpsDeveloperCertificate / WithHttpsCertificate).
        Assert.True(floci.Resource.TryGetLastAnnotation(out HttpsCertificateConfigurationCallbackAnnotation? annotation));

        var envVars = await InvokeCertificateCallbackAsync(builder, floci.Resource, annotation!);

        Assert.Equal("true", envVars[enabled].ToString());
        Assert.Equal("false", envVars[selfSigned].ToString());
        Assert.Equal("/aspire/cert.pem", Assert.IsType<ReferenceExpression>(envVars[certPath]).ValueExpression);
        Assert.Equal("/aspire/key.pem", Assert.IsType<ReferenceExpression>(envVars[keyPath]).ValueExpression);
    }

    [Fact]
    public async Task AnAspireProvidedCertificateIsMappedOntoTheAzureImagesTlsEnvironmentVariables()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var floci = builder.AddFlociAzure("floci-az");

        Assert.True(floci.Resource.TryGetLastAnnotation(out HttpsCertificateConfigurationCallbackAnnotation? annotation));

        var envVars = await InvokeCertificateCallbackAsync(builder, floci.Resource, annotation!);

        Assert.Equal("true", envVars["FLOCI_AZ_TLS_ENABLED"].ToString());
        Assert.Equal("false", envVars["FLOCI_AZ_TLS_SELF_SIGNED"].ToString());
        Assert.Equal("/aspire/cert.pem", Assert.IsType<ReferenceExpression>(envVars["FLOCI_AZ_TLS_CERT_PATH"]).ValueExpression);
        Assert.Equal("/aspire/key.pem", Assert.IsType<ReferenceExpression>(envVars["FLOCI_AZ_TLS_KEY_PATH"]).ValueExpression);
    }

    [Fact]
    public async Task TheFlociUIEndpointStaysHttpWhenTheEmulatorGoesHttps()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociAws("floci")
            .WithHttpsDeveloperCertificate()
            .WithFlociUI();

        using var app = builder.Build();
        await PublishBeforeStartAsync(builder, app);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var floci = appModel.Resources.OfType<FlociAwsContainerResource>().Single();
        var ui = appModel.Resources.OfType<FlociUIContainerResource>().Single();

        // The emulator's own endpoint is https...
        Assert.True(floci.TryGetLastAnnotation(out EndpointAnnotation? endpoint));
        Assert.Equal("https", endpoint.UriScheme);

        // ...but the UI must keep talking plain HTTP on the same port. It reaches the emulator by
        // container-network name, which no host-issued certificate covers, so https here would fail
        // hostname validation and the console could not connect.
        var envVars = await ResolveEnvironmentAsync(builder, ui);
        var uiEndpoint = Assert.IsType<ReferenceExpression>(envVars[FlociUIContainerResource.EndpointEnvVar]);
        Assert.StartsWith("http://", uiEndpoint.ValueExpression);
        Assert.DoesNotContain("scheme", uiEndpoint.ValueExpression);
    }

    [Fact]
    public void TheGcpEmulatorDoesNotRegisterCertificateConfiguration()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var floci = builder.AddFlociGcp("floci-gcp");

        // floci/floci-gcp has no HTTPS listener, so there is no certificate callback to honour and
        // no way for a configured certificate to take effect.
        Assert.False(floci.Resource.TryGetLastAnnotation(out HttpsCertificateConfigurationCallbackAnnotation? _));
    }

    [Fact]
    public async Task TheConnectionStringFollowsTheEndpointScheme()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddFlociAws("floci").WithHttpsDeveloperCertificate();

        using var app = builder.Build();
        await PublishBeforeStartAsync(builder, app);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<FlociAwsContainerResource>().Single();

        // The scheme is an endpoint expression, not a literal, so switching the endpoint to https is
        // enough — every consumer of ConnectionStringExpression follows automatically.
        Assert.Contains("floci.bindings.aws.scheme", resource.ConnectionStringExpression.ValueExpression);
    }

    private static async Task<Dictionary<string, object>> InvokeCertificateCallbackAsync(
        IDistributedApplicationBuilder builder,
        IResource resource,
        HttpsCertificateConfigurationCallbackAnnotation annotation)
    {
        var envVars = new Dictionary<string, object>();

        await annotation.Callback(new HttpsCertificateConfigurationCallbackAnnotationContext
        {
            Arguments = [],
            CancellationToken = TestContext.Current.CancellationToken,
            CertificatePath = ReferenceExpression.Create($"/aspire/cert.pem"),
            KeyPath = ReferenceExpression.Create($"/aspire/key.pem"),
            PfxPath = ReferenceExpression.Create($"/aspire/cert.pfx"),
            EnvironmentVariables = envVars,
            ExecutionContext = builder.ExecutionContext,
            Password = null,
            Resource = resource,
            CertificateWithKeyPath = ReferenceExpression.Create($"/aspire/cert-key.pem")
        });

        return envVars;
    }

    private static async Task<Dictionary<string, object>> ResolveEnvironmentAsync(
        IDistributedApplicationBuilder builder,
        IResource resource)
    {
        Assert.True(resource.TryGetAnnotationsOfType(out IEnumerable<EnvironmentCallbackAnnotation>? envAnnotations));

        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);
        foreach (var annotation in envAnnotations!)
        {
            await annotation.Callback(context);
        }

        return envVars;
    }

    private static async Task PublishBeforeStartAsync(IDistributedApplicationBuilder builder, DistributedApplication app)
    {
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var eventing = (DistributedApplicationEventing)builder.Eventing;

        // Aspire's own DCP handlers throw in unit-test environments without the CLI/dashboard
        // binaries. BlockingConcurrent still runs ours; only that expected failure is swallowed.
        try
        {
            await eventing.PublishAsync(
                new BeforeStartEvent(app.Services, appModel),
                EventDispatchBehavior.BlockingConcurrent,
                TestContext.Current.CancellationToken);
        }
        catch (OptionsValidationException)
        {
        }
    }
}
