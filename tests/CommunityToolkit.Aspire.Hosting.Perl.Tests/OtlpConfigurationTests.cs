using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class OtlpConfigurationTests
{
    private const string GrpcEndpoint = "http://localhost:4317";
    private const string HttpEndpoint = "http://localhost:4318";

    [Theory]
    [InlineData(null)]
    [InlineData(GrpcEndpoint)]
    public async Task AddPerlScript_WithNoHttpOtlpEndpoint_DoesNotConfigurePerlOtlpExport(string? grpcEndpoint)
    {
        var builder = CreateBuilder(grpcEndpoint, httpEndpoint: null);
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();
        var environment = await perlApp.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);

        Assert.DoesNotContain("OTEL_EXPORTER_OTLP_ENDPOINT", environment.Keys);
        Assert.DoesNotContain("OTEL_EXPORTER_OTLP_PROTOCOL", environment.Keys);
        Assert.DoesNotContain(environment.Keys, key => key.StartsWith("OTEL_PERL_", StringComparison.Ordinal));
        Assert.DoesNotContain("OTEL_TRACES_EXPORTER", environment.Keys);
        Assert.DoesNotContain("OTEL_LOGS_EXPORTER", environment.Keys);
        Assert.DoesNotContain("OTEL_METRICS_EXPORTER", environment.Keys);
        AssertRequiredProtocol(perlApp.Resource);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(GrpcEndpoint)]
    public async Task AddPerlScript_WithHttpOtlpEndpoint_UsesMatchingHttpJsonConfiguration(string? grpcEndpoint)
    {
        var builder = CreateBuilder(grpcEndpoint, HttpEndpoint);
        builder.Configuration["AppHost:OtlpApiKey"] = "test-key";
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();
        var environment = await perlApp.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);

        Assert.Equal(HttpEndpoint, environment["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        Assert.Equal("http/json", environment["OTEL_EXPORTER_OTLP_PROTOCOL"]);
        Assert.Equal(environment["OTEL_EXPORTER_OTLP_ENDPOINT"], environment["OTEL_PERL_EXPORTER_OTLP_ENDPOINT"]);
        Assert.Equal(environment["OTEL_EXPORTER_OTLP_PROTOCOL"], environment["OTEL_PERL_EXPORTER_OTLP_PROTOCOL"]);
        Assert.Equal(environment["OTEL_EXPORTER_OTLP_HEADERS"], environment["OTEL_PERL_EXPORTER_OTLP_HEADERS"]);
        Assert.All(
            new[] { "TRACES", "LOGS", "METRICS" },
            signal =>
            {
                Assert.Equal("otlp", environment[$"OTEL_{signal}_EXPORTER"]);
                Assert.Equal("otlp", environment[$"OTEL_PERL_{signal}_EXPORTER"]);
            });
        AssertRequiredProtocol(perlApp.Resource);
    }

    [Theory]
    [InlineData("ENDPOINT", "http://collector.test:4318")]
    [InlineData("PROTOCOL", "http/protobuf")]
    [InlineData("HEADERS", "authorization=collector-test-key")]
    public async Task AddPerlScript_WithStandardOtlpOverride_PerlConfigurationUsesOverride(string setting, string value)
    {
        var builder = CreateBuilder(GrpcEndpoint, HttpEndpoint);
        builder.Configuration["AppHost:OtlpApiKey"] = "dashboard-test-key";
        var standardName = $"OTEL_EXPORTER_OTLP_{setting}";
        var perlName = $"OTEL_PERL_EXPORTER_OTLP_{setting}";
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithEnvironment(standardName, value);

        using var app = builder.Build();
        var environment = await perlApp.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);

        Assert.Equal(value, environment[standardName]);
        Assert.Equal(value, environment[perlName]);
    }

    [Theory]
    [InlineData("TRACES")]
    [InlineData("LOGS")]
    [InlineData("METRICS")]
    public async Task AddPerlScript_WithStandardExporterOverride_PerlConfigurationUsesOverride(string signal)
    {
        var builder = CreateBuilder(GrpcEndpoint, HttpEndpoint);
        var standardName = $"OTEL_{signal}_EXPORTER";
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithEnvironment(standardName, "none");

        using var app = builder.Build();
        var environment = await perlApp.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);

        Assert.Equal("none", environment[standardName]);
        Assert.Equal("none", environment[$"OTEL_PERL_{signal}_EXPORTER"]);
    }

    [Theory]
    [InlineData("http://collector.test:4318", "http/protobuf", "authorization=collector-test-key")]
    [InlineData("http://alternate-collector.test:14318", "http/json", "authorization=alternate-test-key")]
    public async Task AddPerlScript_WithStandardOtlpOverridesInCallback_PerlConfigurationUsesFinalValues(
        string endpoint, string protocol, string headers)
    {
        var builder = CreateBuilder(GrpcEndpoint, HttpEndpoint);
        builder.Configuration["AppHost:OtlpApiKey"] = "dashboard-test-key";
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint;
                context.EnvironmentVariables["OTEL_EXPORTER_OTLP_PROTOCOL"] = protocol;
                context.EnvironmentVariables["OTEL_EXPORTER_OTLP_HEADERS"] = headers;
            });

        using var app = builder.Build();
        var environment = await perlApp.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);

        Assert.Equal(endpoint, environment["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        Assert.Equal(protocol, environment["OTEL_EXPORTER_OTLP_PROTOCOL"]);
        Assert.Equal(headers, environment["OTEL_EXPORTER_OTLP_HEADERS"]);
        Assert.Equal(environment["OTEL_EXPORTER_OTLP_ENDPOINT"], environment["OTEL_PERL_EXPORTER_OTLP_ENDPOINT"]);
        Assert.Equal(environment["OTEL_EXPORTER_OTLP_PROTOCOL"], environment["OTEL_PERL_EXPORTER_OTLP_PROTOCOL"]);
        Assert.Equal(environment["OTEL_EXPORTER_OTLP_HEADERS"], environment["OTEL_PERL_EXPORTER_OTLP_HEADERS"]);
    }

    [Fact]
    public async Task AddPerlScript_WithExplicitPerlOtlpOverrides_PreservesPerlSpecificConfiguration()
    {
        var builder = CreateBuilder(GrpcEndpoint, HttpEndpoint);
        builder.Configuration["AppHost:OtlpApiKey"] = "dashboard-test-key";
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithEnvironment("OTEL_PERL_EXPORTER_OTLP_ENDPOINT", "http://collector.test:4318")
            .WithEnvironment("OTEL_PERL_EXPORTER_OTLP_PROTOCOL", "http/protobuf")
            .WithEnvironment("OTEL_PERL_EXPORTER_OTLP_HEADERS", "authorization=collector-test-key");

        using var app = builder.Build();
        var environment = await perlApp.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);

        Assert.Equal("http://collector.test:4318", environment["OTEL_PERL_EXPORTER_OTLP_ENDPOINT"]);
        Assert.Equal("http/protobuf", environment["OTEL_PERL_EXPORTER_OTLP_PROTOCOL"]);
        Assert.Equal("authorization=collector-test-key", environment["OTEL_PERL_EXPORTER_OTLP_HEADERS"]);
        Assert.Equal(HttpEndpoint, environment["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        Assert.Equal("http/json", environment["OTEL_EXPORTER_OTLP_PROTOCOL"]);
        Assert.Equal("x-otlp-api-key=dashboard-test-key", environment["OTEL_EXPORTER_OTLP_HEADERS"]);
    }

    [Fact]
    public async Task AddPerlScript_InPublishMode_LeavesOtlpEndpointToDeploymentTarget()
    {
        var builder = DistributedApplication.CreateBuilder();
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl");

        var environment = await perlApp.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish);

        Assert.DoesNotContain("OTEL_EXPORTER_OTLP_ENDPOINT", environment.Keys);
        Assert.DoesNotContain("OTEL_EXPORTER_OTLP_PROTOCOL", environment.Keys);
        Assert.DoesNotContain("OTEL_PERL_EXPORTER_OTLP_ENDPOINT", environment.Keys);
        Assert.DoesNotContain("OTEL_PERL_EXPORTER_OTLP_PROTOCOL", environment.Keys);
        Assert.DoesNotContain("OTEL_EXPORTER_OTLP_HEADERS", environment.Keys);
        Assert.DoesNotContain("OTEL_PERL_EXPORTER_OTLP_HEADERS", environment.Keys);
        Assert.All(
            new[] { "TRACES", "LOGS", "METRICS" },
            signal =>
            {
                Assert.Equal("otlp", environment[$"OTEL_{signal}_EXPORTER"]);
                Assert.Equal("otlp", environment[$"OTEL_PERL_{signal}_EXPORTER"]);
            });
        AssertRequiredProtocol(perlApp.Resource);
    }

    private static IDistributedApplicationBuilder CreateBuilder(string? grpcEndpoint, string? httpEndpoint)
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = grpcEndpoint;
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL"] = httpEndpoint;
        builder.Configuration["DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"] = null;
        builder.Configuration["DOTNET_DASHBOARD_OTLP_HTTP_ENDPOINT_URL"] = null;
        return builder;
    }

    private static void AssertRequiredProtocol(PerlAppResource resource)
    {
        var annotation = Assert.Single(resource.Annotations.OfType<OtlpExporterAnnotation>());
        Assert.Equal(OtlpProtocol.HttpJson, annotation.RequiredProtocol);
    }
}
