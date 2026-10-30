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
        var loki = builder.AddLoki("loki")
            .WithHttpHostPort(httpPort)
            .WithGrpcHostPort(grpcPort);
        using var app = builder.Build();

        Assert.Same(loki.Resource, Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources));
        Assert.Equal("loki", loki.Resource.Name);
        var image = Assert.Single(loki.Resource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(LokiContainerImageTags.Registry, image.Registry);
        Assert.Equal(LokiContainerImageTags.Image, image.Image);
        Assert.Equal(LokiContainerImageTags.Tag, image.Tag);
        Assert.Equal(2, loki.Resource.Annotations.OfType<EndpointAnnotation>().Count());

        var http = loki.Resource.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "http");
        Assert.Equal("http", http.UriScheme);
        Assert.Equal(3100, http.TargetPort);
        Assert.Equal(httpPort, http.Port);
        Assert.True(http.IsProxied);

        var grpc = loki.Resource.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc");
        Assert.Equal("http", grpc.UriScheme);
        Assert.Equal("http2", grpc.Transport);
        Assert.Equal(9096, grpc.TargetPort);
        Assert.Equal(grpcPort, grpc.Port);
        Assert.True(grpc.IsProxied);
        Assert.Empty(loki.Resource.Annotations.OfType<ContainerMountAnnotation>());

        var healthCheck = Assert.Single(loki.Resource.Annotations.OfType<HealthCheckAnnotation>());
        Assert.Contains(app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations,
            registration => registration.Name == healthCheck.Key);
    }

    [Fact]
    public void CanConfigureContainerAfterAddingLoki()
    {
        var builder = DistributedApplication.CreateBuilder();
        var loki = builder.AddLoki("loki")
            .WithImageRegistry("registry.example.com")
            .WithImage("custom/loki")
            .WithImageTag("3.6.1");
        using var app = builder.Build();

        var image = Assert.Single(loki.Resource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("registry.example.com", image.Registry);
        Assert.Equal("custom/loki", image.Image);
        Assert.Equal("3.6.1", image.Tag);
        Assert.Equal(2, loki.Resource.Annotations.OfType<EndpointAnnotation>().Count());
        Assert.Single(loki.Resource.Annotations.OfType<HealthCheckAnnotation>());
    }

    [Fact]
    public void EndpointsResolveIndependently()
    {
        var builder = DistributedApplication.CreateBuilder();
        var loki = builder.AddLoki("loki")
            .WithEndpoint("http", endpoint => endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 13100))
            .WithEndpoint("grpc", endpoint => endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "grpc.example.com", 19096));
        using var app = builder.Build();

        Assert.Equal("http", loki.Resource.HttpEndpoint.EndpointName);
        Assert.Equal("grpc", loki.Resource.GrpcEndpoint.EndpointName);
        Assert.Equal("http://grpc.example.com:19096", loki.Resource.GrpcEndpoint.Url);
        Assert.Equal("http://localhost:13100", loki.Resource.HttpEndpoint.Url);
        Assert.Same(loki.Resource.HttpEndpoint, loki.Resource.HttpEndpoint);
        Assert.Same(loki.Resource.GrpcEndpoint, loki.Resource.GrpcEndpoint);
        Assert.IsNotAssignableFrom<IResourceWithConnectionString>(loki.Resource);
    }

    [Fact]
    public async Task WithReferenceSuppliesNamedServiceDiscoveryEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder();
        var loki = builder.AddLoki("loki")
            .WithEndpoint("http", endpoint => endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 13100))
            .WithEndpoint("grpc", endpoint => endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 19096));
        var consumer = builder.AddExecutable("consumer", "dotnet", ".").WithReference(loki);
        using var app = builder.Build();

        var environment = await consumer.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal("http://localhost:13100", environment["services__loki__http__0"]);
        Assert.Equal("http://localhost:19096", environment["services__loki__grpc__0"]);
        Assert.DoesNotContain("ConnectionStrings__loki", environment.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(23100)]
    public void HostPortConfigurationCanBeChangedOrCleared(int? port)
    {
        var builder = DistributedApplication.CreateBuilder();
        var loki = builder.AddLoki("loki").WithHttpHostPort(13100).WithGrpcHostPort(19096);
        using var app = builder.Build();

        Assert.Same(loki, loki.WithHttpHostPort(port));
        Assert.Same(loki, loki.WithGrpcHostPort(port + 1));

        var endpoints = loki.Resource.Annotations.OfType<EndpointAnnotation>().ToDictionary(endpoint => endpoint.Name);
        Assert.Equal(port, endpoints["http"].Port);
        Assert.Equal(port + 1, endpoints["grpc"].Port);
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
        Assert.Equal("/loki", mount.Target);
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
        Assert.Equal("/loki", mount.Target);
        Assert.False(mount.IsReadOnly);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WithConfigFileCopiesConfigurationInRunAndPublishModes(bool publish, bool absolutePath)
    {
        var builder = DistributedApplication.CreateBuilder(publish ? ["--publisher", "manifest"] : []);
        var loki = builder.AddLoki("loki");
        using var app = builder.Build();

        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "auth_enabled: false");
            var source = absolutePath ? path : Path.GetRelativePath(builder.AppHostDirectory, path);
            Assert.Same(loki, loki.WithConfigFile(source));

            Assert.Empty(loki.Resource.Annotations.OfType<ContainerMountAnnotation>());
            var files = Assert.Single(loki.Resource.Annotations.OfType<ContainerFileSystemCallbackAnnotation>());
            Assert.Equal("/etc/loki", files.DestinationPath);
            var items = await files.Callback(new ContainerFileSystemCallbackContext
            {
                Services = app.Services,
                Model = loki.Resource
            }, TestContext.Current.CancellationToken);
            var file = Assert.IsType<ContainerFile>(Assert.Single(items));
            Assert.Equal(path, file.SourcePath);
            Assert.Equal("local-config.yaml", file.Name);
            Assert.Equal(["-config.file=/etc/loki/local-config.yaml"], await loki.Resource.GetArgumentListAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MountHelpersValidateArguments()
    {
        IResourceBuilder<LokiResource> missing = null!;
        Assert.Throws<ArgumentNullException>(() => missing.WithDataVolume());
        Assert.Throws<ArgumentNullException>(() => missing.WithDataBindMount("data"));
        Assert.Throws<ArgumentNullException>(() => missing.WithConfigFile("loki.yaml"));
        Assert.Throws<ArgumentNullException>(() => missing.WithHttpHostPort(null));
        Assert.Throws<ArgumentNullException>(() => missing.WithGrpcHostPort(null));

        var builder = DistributedApplication.CreateBuilder();
        var loki = builder.AddLoki("loki");
        using var app = builder.Build();
        Assert.Throws<ArgumentNullException>(() => loki.WithDataBindMount(null!));
        Assert.Throws<ArgumentException>(() => loki.WithDataBindMount(string.Empty));
        Assert.Throws<ArgumentNullException>(() => loki.WithConfigFile(null!));
        Assert.Throws<ArgumentException>(() => loki.WithConfigFile(string.Empty));
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
        Assert.Equal($"{LokiContainerImageTags.Registry}/{LokiContainerImageTags.Image}:{LokiContainerImageTags.Tag}", resource.GetProperty("image").GetString());
        Assert.False(resource.TryGetProperty("connectionString", out _));
        var bindings = resource.GetProperty("bindings");
        Assert.Equal(3100, bindings.GetProperty("http").GetProperty("targetPort").GetInt32());
        Assert.Equal("http", bindings.GetProperty("http").GetProperty("scheme").GetString());
        Assert.Equal(9096, bindings.GetProperty("grpc").GetProperty("targetPort").GetInt32());
        Assert.Equal("http", bindings.GetProperty("grpc").GetProperty("scheme").GetString());
        Assert.Equal("http2", bindings.GetProperty("grpc").GetProperty("transport").GetString());
        if (persistData)
        {
            var volume = Assert.Single(resource.GetProperty("volumes").EnumerateArray());
            Assert.Equal("/loki", volume.GetProperty("target").GetString());
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
        using var client = new HttpClient { BaseAddress = new Uri(loki.Resource.HttpEndpoint.Url) };
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