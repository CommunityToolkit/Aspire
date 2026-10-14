using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Publishing;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CommunityToolkit.Aspire.Hosting.Loki.Tests;

public class ResourceCreationTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void AddLokiValidatesBuilderAndName()
    {
        IDistributedApplicationBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddLoki("loki"));

        builder = DistributedApplication.CreateBuilder();
        using var app = builder.Build();
        Assert.Throws<ArgumentNullException>(() => builder.AddLoki(null!));
        Assert.Throws<ArgumentException>(() => builder.AddLoki(string.Empty));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(13100, 19096)]
    public void AddLokiConfiguresContainerAndEndpoints(int? httpPort, int? grpcPort)
    {
        var builder = DistributedApplication.CreateBuilder();
        var loki = builder.AddLoki("loki", settings =>
        {
            settings.HttpPort = httpPort;
            settings.GrpcPort = grpcPort;
        });
        using var app = builder.Build();

        Assert.Same(loki.Resource, Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources));
        Assert.Equal("loki", loki.Resource.Name);
        var image = Assert.Single(loki.Resource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("docker.io", image.Registry);
        Assert.Equal("grafana/loki", image.Image);
        Assert.Equal("3.6", image.Tag);
        Assert.Equal(2, loki.Resource.Annotations.OfType<EndpointAnnotation>().Count());

        var http = loki.Resource.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "http");
        Assert.Equal("http", http.UriScheme);
        Assert.Equal(3100, http.TargetPort);
        Assert.Equal(httpPort, http.Port);
        Assert.True(http.IsProxied);

        var grpc = loki.Resource.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc");
        Assert.Equal("tcp", grpc.UriScheme);
        Assert.Equal(9096, grpc.TargetPort);
        Assert.Equal(grpcPort, grpc.Port);
        Assert.True(grpc.IsProxied);
        Assert.Empty(loki.Resource.Annotations.OfType<ContainerMountAnnotation>());

        var healthCheck = Assert.Single(loki.Resource.Annotations.OfType<HealthCheckAnnotation>());
        Assert.Contains(app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations,
            registration => registration.Name == healthCheck.Key);
    }

    [Fact]
    public void CanConfigureLokiSettings()
    {
        var builder = DistributedApplication.CreateBuilder();
        var loki = builder.AddLoki("loki", settings =>
        {
            settings.Registry = "registry.example.com";
            settings.Image = "custom/loki";
            settings.LokiTag = "3.6.1";
            settings.EnableGrpcEndpoint = false;
            settings.DisableHealthcheck = true;
            Assert.Equal("registry.example.com/custom/loki", settings.LokiImage);
        });
        using var app = builder.Build();

        var image = Assert.Single(loki.Resource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("registry.example.com", image.Registry);
        Assert.Equal("custom/loki", image.Image);
        Assert.Equal("3.6.1", image.Tag);
        var endpoint = Assert.Single(loki.Resource.Annotations.OfType<EndpointAnnotation>());
        Assert.Equal("http", endpoint.Name);
        Assert.Empty(loki.Resource.Annotations.OfType<HealthCheckAnnotation>());
    }

    [Fact]
    public async Task ConnectionStringAndPropertiesResolveHttpEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();
        var loki = builder.AddLoki("loki")
            .WithEndpoint("http", endpoint => endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 13100));
        using var app = builder.Build();

        Assert.Equal("http", loki.Resource.PrimaryEndpoint.EndpointName);
        Assert.Equal("grpc", loki.Resource.GrpcEndpoint.EndpointName);
        Assert.Equal("http://localhost:13100", await loki.Resource.ConnectionStringExpression.GetValueAsync(default));
        Assert.Equal("{loki.bindings.http.url}", loki.Resource.ConnectionStringExpression.ValueExpression);

        var properties = ((IResourceWithConnectionString)loki.Resource).GetConnectionProperties().ToDictionary();
        Assert.Equal(3, properties.Count);
        Assert.Equal("localhost", await properties["Host"].GetValueAsync(default));
        Assert.Equal("13100", await properties["Port"].GetValueAsync(default));
        Assert.Equal("http://localhost:13100", await properties["Uri"].GetValueAsync(default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("loki-data")]
    public void WithDataVolumeMountsDefaultStorageDirectory(string? volumeName)
    {
        var builder = DistributedApplication.CreateBuilder();
        var loki = builder.AddLoki("loki");
        using var app = builder.Build();

        Assert.Same(loki, loki.WithDataVolume(volumeName));

        var mount = Assert.Single(loki.Resource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal(ContainerMountType.Volume, mount.Type);
        Assert.Equal("/tmp/loki", mount.Target);
        Assert.False(mount.IsReadOnly);
        Assert.Equal(volumeName ?? VolumeNameGenerator.Generate(loki, "data"), mount.Source);
    }

    [Fact]
    public void WithDataVolumeGeneratesDistinctNamesForDifferentResources()
    {
        var builder = DistributedApplication.CreateBuilder();
        var first = builder.AddLoki("first").WithDataVolume();
        var second = builder.AddLoki("second").WithDataVolume();
        using var app = builder.Build();

        Assert.NotEqual(
            Assert.Single(first.Resource.Annotations.OfType<ContainerMountAnnotation>()).Source,
            Assert.Single(second.Resource.Annotations.OfType<ContainerMountAnnotation>()).Source);
    }

    [Fact]
    public void WithDataBindMountResolvesHostPath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var loki = builder.AddLoki("loki");
        using var app = builder.Build();

        Assert.Same(loki, loki.WithDataBindMount("loki-data"));

        var mount = Assert.Single(loki.Resource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal(ContainerMountType.BindMount, mount.Type);
        Assert.Equal(Path.GetFullPath("loki-data", builder.AppHostDirectory), mount.Source);
        Assert.Equal("/tmp/loki", mount.Target);
        Assert.False(mount.IsReadOnly);
    }

    [Fact]
    public async Task WithConfigBindMountIsReadOnly()
    {
        var builder = DistributedApplication.CreateBuilder();
        var loki = builder.AddLoki("loki");
        using var app = builder.Build();

        Assert.Same(loki, loki.WithConfigBindMount("loki.yaml"));

        var mount = Assert.Single(loki.Resource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal(ContainerMountType.BindMount, mount.Type);
        Assert.Equal(Path.GetFullPath("loki.yaml", builder.AppHostDirectory), mount.Source);
        Assert.Equal("/etc/loki/local-config.yaml", mount.Target);
        Assert.True(mount.IsReadOnly);
        Assert.Equal(["-config.file=/etc/loki/local-config.yaml"], await loki.Resource.GetArgumentListAsync());
    }

    [Fact]
    public void MountHelpersValidateArguments()
    {
        IResourceBuilder<LokiResource> missing = null!;
        Assert.Throws<ArgumentNullException>(() => missing.WithDataVolume());
        Assert.Throws<ArgumentNullException>(() => missing.WithDataBindMount("data"));
        Assert.Throws<ArgumentNullException>(() => missing.WithConfigBindMount("loki.yaml"));

        var builder = DistributedApplication.CreateBuilder();
        var loki = builder.AddLoki("loki");
        using var app = builder.Build();
        Assert.Throws<ArgumentNullException>(() => loki.WithDataBindMount(null!));
        Assert.Throws<ArgumentException>(() => loki.WithDataBindMount(string.Empty));
        Assert.Throws<ArgumentNullException>(() => loki.WithConfigBindMount(null!));
        Assert.Throws<ArgumentException>(() => loki.WithConfigBindMount(string.Empty));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishPreservesContainerBindingsAndOptionalStorage(bool persistData)
    {
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        var loki = builder.AddLoki("loki");
        if (persistData)
        {
            loki.WithDataVolume("loki-data");
        }
        using var app = builder.Build();
        Assert.True(builder.ExecutionContext.IsPublishMode);

        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            var context = new ManifestPublishingContext(builder.ExecutionContext,
                Path.Combine(builder.AppHostDirectory, "manifest.json"), writer, default);
            await context.WriteContainerAsync(loki.Resource);
            writer.WriteEndObject();
        }

        using var manifest = JsonDocument.Parse(output.ToArray());
        var resource = manifest.RootElement;
        Assert.Equal("container.v0", resource.GetProperty("type").GetString());
        Assert.Equal("docker.io/grafana/loki:3.6", resource.GetProperty("image").GetString());
        Assert.Equal("{loki.bindings.http.url}", resource.GetProperty("connectionString").GetString());
        var bindings = resource.GetProperty("bindings");
        Assert.Equal(3100, bindings.GetProperty("http").GetProperty("targetPort").GetInt32());
        Assert.Equal("http", bindings.GetProperty("http").GetProperty("scheme").GetString());
        Assert.Equal(9096, bindings.GetProperty("grpc").GetProperty("targetPort").GetInt32());
        Assert.Equal("tcp", bindings.GetProperty("grpc").GetProperty("scheme").GetString());
        if (persistData)
        {
            var volume = Assert.Single(resource.GetProperty("volumes").EnumerateArray());
            Assert.Equal("/tmp/loki", volume.GetProperty("target").GetString());
            Assert.False(volume.GetProperty("readOnly").GetBoolean());
        }
        else
        {
            Assert.False(resource.TryGetProperty("volumes", out _));
        }
    }

    [Fact]
    [RequiresDocker]
    public async Task ResourceStartsAndStoresLogs()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var cancellationToken = cancellation.Token;
        using var builder = TestDistributedApplicationBuilder.Create()
            .WithTestAndResourceLogging(testOutputHelper);
        var loki = builder.AddLoki("loki");
        using var app = builder.Build();
        await app.StartAsync(cancellationToken);
        await app.Services.GetRequiredService<ResourceNotificationService>()
            .WaitForResourceHealthyAsync("loki", cancellationToken);
        var connectionString = await loki.Resource.ConnectionStringExpression.GetValueAsync(cancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri(connectionString!) };
        using var ready = await client.GetAsync("/ready", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        var testId = Guid.NewGuid().ToString("N");
        var timestamp = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000).ToString(CultureInfo.InvariantCulture);
        using var push = await client.PostAsJsonAsync("/loki/api/v1/push", new
        {
            streams = new[]
            {
                new
                {
                    stream = new { test_id = testId },
                    values = new[] { new[] { timestamp, "Loki integration test" } }
                }
            }
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, push.StatusCode);

        var query = Uri.EscapeDataString($"{{test_id=\"{testId}\"}}");
        using var response = await client.GetAsync($"/loki/api/v1/query_range?query={query}", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var result = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        Assert.Equal("success", result.RootElement.GetProperty("status").GetString());
        var stream = Assert.Single(result.RootElement.GetProperty("data").GetProperty("result").EnumerateArray());
        var value = Assert.Single(stream.GetProperty("values").EnumerateArray());
        Assert.Equal("Loki integration test", value[1].GetString());
    }
}