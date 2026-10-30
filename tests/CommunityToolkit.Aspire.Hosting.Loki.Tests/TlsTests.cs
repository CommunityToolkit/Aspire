using Aspire.Hosting;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Utils;

#pragma warning disable ASPIRECERTIFICATES001

namespace CommunityToolkit.Aspire.Hosting.Loki.Tests;

public class TlsTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CertificateChoiceUpdatesOnlyHttpEndpoint(bool useHttps)
    {
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);
        var loki = builder.AddLoki("loki");
        if (useHttps)
        {
            loki.WithHttpsDeveloperCertificate();
        }

        else
        {
            loki.WithoutHttpsCertificate();
        }

        using var app = builder.Build();
        await builder.Eventing.PublishAsync(
            new BeforeStartEvent(app.Services, app.Services.GetRequiredService<DistributedApplicationModel>()),
            EventDispatchBehavior.BlockingSequential,
            TestContext.Current.CancellationToken);

        var endpoints = loki.Resource.Annotations.OfType<EndpointAnnotation>().ToDictionary(endpoint => endpoint.Name);
        Assert.Equal(useHttps ? "https" : "http", endpoints["http"].UriScheme);
        Assert.Equal("http", endpoints["grpc"].UriScheme);
        Assert.Equal("http2", endpoints["grpc"].Transport);
        Assert.Single(loki.Resource.Annotations.OfType<HealthCheckAnnotation>());
    }

    [Fact]
    public async Task CertificateCallbackPassesStructuredPathsToLoki()
    {
        var builder = DistributedApplication.CreateBuilder();
        var loki = builder.AddLoki("loki");
        using var app = builder.Build();
        var callback = Assert.Single(loki.Resource.Annotations.OfType<HttpsCertificateConfigurationCallbackAnnotation>());
        var arguments = new List<object>();

        await callback.Callback(new HttpsCertificateConfigurationCallbackAnnotationContext
        {
            Arguments = arguments,
            CancellationToken = TestContext.Current.CancellationToken,
            CertificatePath = ReferenceExpression.Create($"/aspire/cert.pem"),
            KeyPath = ReferenceExpression.Create($"/aspire/key.pem"),
            PfxPath = ReferenceExpression.Create($"/aspire/cert.pfx"),
            CertificateWithKeyPath = ReferenceExpression.Create($"/aspire/cert-key.pem"),
            EnvironmentVariables = new Dictionary<string, object>(),
            ExecutionContext = builder.ExecutionContext,
            Password = null,
            Resource = loki.Resource
        });

        Assert.Collection(arguments,
            argument => Assert.Equal("-server.http-tls-cert-path=/aspire/cert.pem", Assert.IsType<ReferenceExpression>(argument).ValueExpression),
            argument => Assert.Equal("-server.http-tls-key-path=/aspire/key.pem", Assert.IsType<ReferenceExpression>(argument).ValueExpression));
    }
}

#pragma warning restore ASPIRECERTIFICATES001
