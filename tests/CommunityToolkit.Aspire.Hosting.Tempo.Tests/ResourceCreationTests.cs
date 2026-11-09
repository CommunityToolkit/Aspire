using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Publishing;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using YamlDotNet.RepresentationModel;

namespace CommunityToolkit.Aspire.Hosting.Tempo.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void AddTempoValidatesBuilderAndName()
    {
        IDistributedApplicationBuilder missing = null!;
        Assert.Throws<ArgumentNullException>(() => missing.AddTempo("tempo"));
        var builder = DistributedApplication.CreateBuilder();
        using var app = builder.Build();
        Assert.Throws<ArgumentNullException>(() => builder.AddTempo(null!));
        Assert.Throws<ArgumentException>(() => builder.AddTempo(string.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(13200)]
    public void AddTempoConfiguresContainerAndEndpoints(int? port)
    {
        var builder = DistributedApplication.CreateBuilder();
        var tempo = builder.AddTempo("tempo")
            .WithHttpHostPort(port).WithGrpcHostPort(port + 1)
            .WithOtlpGrpcHostPort(port + 2).WithOtlpHttpHostPort(port + 3);
        using var app = builder.Build();

        Assert.Same(tempo.Resource, Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources));
        Assert.Equal("tempo", tempo.Resource.Name);
        var image = Assert.Single(tempo.Resource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(TempoContainerImageTags.Registry, image.Registry);
        Assert.Equal(TempoContainerImageTags.Image, image.Image);
        Assert.Equal(TempoContainerImageTags.Tag, image.Tag);
        var endpoints = tempo.Resource.Annotations.OfType<EndpointAnnotation>().ToDictionary(endpoint => endpoint.Name);
        Assert.Equal(4, endpoints.Count);
        AssertEndpoint(endpoints["http"], 3200, port, "http");
        AssertEndpoint(endpoints["grpc"], 9095, port + 1, "http2");
        AssertEndpoint(endpoints["otlp-grpc"], 4317, port + 2, "http2");
        AssertEndpoint(endpoints["otlp-http"], 4318, port + 3, "http");
        Assert.Empty(tempo.Resource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.IsNotAssignableFrom<IResourceWithConnectionString>(tempo.Resource);
        var healthCheck = Assert.Single(tempo.Resource.Annotations.OfType<HealthCheckAnnotation>());
        Assert.Contains(app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations,
            registration => registration.Name == healthCheck.Key);
    }

    private static void AssertEndpoint(EndpointAnnotation endpoint, int targetPort, int? port, string transport)
    {
        Assert.Equal("http", endpoint.UriScheme);
        Assert.Equal(transport, endpoint.Transport);
        Assert.Equal(targetPort, endpoint.TargetPort);
        Assert.Equal(port, endpoint.Port);
        Assert.True(endpoint.IsProxied);
        Assert.False(endpoint.IsExternal);
    }

    [Fact]
    public async Task WithReferenceSuppliesAllServiceDiscoveryEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tempo = builder.AddTempo("tempo");
        var references = new[] { tempo.Resource.HttpEndpoint, tempo.Resource.GrpcEndpoint, tempo.Resource.OtlpGrpcEndpoint, tempo.Resource.OtlpHttpEndpoint };
        var port = 13200;
        foreach (var reference in references)
        {
            var allocatedPort = port++;
            tempo.WithEndpoint(reference.EndpointName, endpoint => endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", allocatedPort));
        }
        var consumer = builder.AddExecutable("consumer", "dotnet", ".").WithReference(tempo);
        using var app = builder.Build();
        var environment = await consumer.Resource.GetEnvironmentVariablesAsync();
        port = 13200;
        foreach (var reference in references)
        {
            Assert.Equal($"http://localhost:{port++}", reference.Url);
            Assert.Equal(reference.Url, environment[$"services__tempo__{reference.EndpointName}__0"]);
        }
        Assert.DoesNotContain("ConnectionStrings__tempo", environment.Keys);
        Assert.Same(tempo.Resource.HttpEndpoint, tempo.Resource.HttpEndpoint);
        Assert.Same(tempo.Resource.GrpcEndpoint, tempo.Resource.GrpcEndpoint);
        Assert.Same(tempo.Resource.OtlpGrpcEndpoint, tempo.Resource.OtlpGrpcEndpoint);
        Assert.Same(tempo.Resource.OtlpHttpEndpoint, tempo.Resource.OtlpHttpEndpoint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(23200)]
    public void HostPortsCanBeChangedOrCleared(int? port)
    {
        var builder = DistributedApplication.CreateBuilder();
        var tempo = builder.AddTempo("tempo").WithHttpHostPort(13200).WithGrpcHostPort(19095)
            .WithOtlpGrpcHostPort(14317).WithOtlpHttpHostPort(14318);
        using var app = builder.Build();
        Assert.Same(tempo, tempo.WithHttpHostPort(port));
        Assert.Same(tempo, tempo.WithGrpcHostPort(port));
        Assert.Same(tempo, tempo.WithOtlpGrpcHostPort(port));
        Assert.Same(tempo, tempo.WithOtlpHttpHostPort(port));
        Assert.All(tempo.Resource.Annotations.OfType<EndpointAnnotation>(), endpoint => Assert.Equal(port, endpoint.Port));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("tempo-data")]
    public void WithDataVolumeMountsDefaultStorageDirectory(string? name)
    {
        var builder = DistributedApplication.CreateBuilder();
        var tempo = builder.AddTempo("tempo");
        var other = builder.AddTempo("other").WithDataVolume();
        using var app = builder.Build();
        Assert.Same(tempo, tempo.WithDataVolume(name));
        var mount = Assert.Single(tempo.Resource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal(ContainerMountType.Volume, mount.Type);
        Assert.Equal("/var/tempo", mount.Target);
        Assert.False(mount.IsReadOnly);
        Assert.Equal(name ?? VolumeNameGenerator.Generate(tempo, "data"), mount.Source);
        Assert.NotEqual(mount.Source, Assert.Single(other.Resource.Annotations.OfType<ContainerMountAnnotation>()).Source);
    }

    [Fact]
    public void WithDataBindMountResolvesHostPath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tempo = builder.AddTempo("tempo");
        using var app = builder.Build();
        Assert.Same(tempo, tempo.WithDataBindMount("tempo-data"));
        var mount = Assert.Single(tempo.Resource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal(ContainerMountType.BindMount, mount.Type);
        Assert.Equal(Path.GetFullPath("tempo-data", builder.AppHostDirectory), mount.Source);
        Assert.Equal("/var/tempo", mount.Target);
        Assert.False(mount.IsReadOnly);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ConfigurationIsCopiedInRunAndPublishModes(bool publish, bool absolutePath)
    {
        var builder = DistributedApplication.CreateBuilder(publish ? ["--publisher", "manifest"] : []);
        var tempo = builder.AddTempo("tempo");
        using var app = builder.Build();
        var defaults = Assert.Single(tempo.Resource.Annotations.OfType<ContainerFileSystemCallbackAnnotation>());
        var defaultItems = await defaults.Callback(new ContainerFileSystemCallbackContext { Services = app.Services, Model = tempo.Resource }, TestContext.Current.CancellationToken);
        var defaultFile = Assert.IsType<ContainerFile>(Assert.Single(defaultItems));
        Assert.Equal("tempo.yaml", defaultFile.Name);
        Assert.Null(defaultFile.SourcePath);
        Assert.NotNull(defaultFile.Contents);
        using var configReader = new StringReader(defaultFile.Contents);
        var yaml = new YamlStream();
        yaml.Load(configReader);
        var root = Assert.IsType<YamlMappingNode>(Assert.Single(yaml.Documents).RootNode);
        var server = GetMapping(root, "server");
        Assert.Equal("3200", GetScalar(server, "http_listen_port"));
        Assert.Equal("9095", GetScalar(server, "grpc_listen_port"));
        var protocols = GetMapping(GetMapping(GetMapping(GetMapping(root, "distributor"), "receivers"), "otlp"), "protocols");
        Assert.Equal("0.0.0.0:4317", GetScalar(GetMapping(protocols, "grpc"), "endpoint"));
        Assert.Equal("0.0.0.0:4318", GetScalar(GetMapping(protocols, "http"), "endpoint"));
        var trace = GetMapping(GetMapping(root, "storage"), "trace");
        Assert.Equal("local", GetScalar(trace, "backend"));
        Assert.Equal("/var/tempo/wal", GetScalar(GetMapping(trace, "wal"), "path"));
        Assert.Equal("/var/tempo/blocks", GetScalar(GetMapping(trace, "local"), "path"));

        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "server:\n  http_listen_port: 3200");
            var source = absolutePath ? path : Path.GetRelativePath(builder.AppHostDirectory, path);
            Assert.Same(tempo, tempo.WithConfigFile(source));
            Assert.Same(tempo, tempo.WithConfigFile(source));
            Assert.Empty(tempo.Resource.Annotations.OfType<ContainerMountAnnotation>());
            var files = Assert.Single(tempo.Resource.Annotations.OfType<ContainerFileSystemCallbackAnnotation>());
            Assert.Equal("/etc/tempo", files.DestinationPath);
            var items = await files.Callback(new ContainerFileSystemCallbackContext { Services = app.Services, Model = tempo.Resource }, TestContext.Current.CancellationToken);
            var file = Assert.IsType<ContainerFile>(Assert.Single(items));
            Assert.Equal(path, file.SourcePath);
            Assert.Equal("tempo.yaml", file.Name);
            Assert.Equal(["-config.file=/etc/tempo/tempo.yaml"], await tempo.Resource.GetArgumentListAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static YamlMappingNode GetMapping(YamlMappingNode parent, string key)
    {
        return Assert.IsType<YamlMappingNode>(parent.Children[new YamlScalarNode(key)]);
    }

    private static string? GetScalar(YamlMappingNode parent, string key)
    {
        return Assert.IsType<YamlScalarNode>(parent.Children[new YamlScalarNode(key)]).Value;
    }

    [Fact]
    public void HelpersValidateArguments()
    {
        IResourceBuilder<TempoResource> missing = null!;
        Assert.Throws<ArgumentNullException>(() => missing.WithHttpHostPort(null));
        Assert.Throws<ArgumentNullException>(() => missing.WithGrpcHostPort(null));
        Assert.Throws<ArgumentNullException>(() => missing.WithOtlpGrpcHostPort(null));
        Assert.Throws<ArgumentNullException>(() => missing.WithOtlpHttpHostPort(null));
        Assert.Throws<ArgumentNullException>(() => missing.WithDataVolume());
        Assert.Throws<ArgumentNullException>(() => missing.WithDataBindMount("data"));
        Assert.Throws<ArgumentNullException>(() => missing.WithConfigFile("tempo.yaml"));
        var builder = DistributedApplication.CreateBuilder();
        var tempo = builder.AddTempo("tempo");
        using var app = builder.Build();
        Assert.Throws<ArgumentNullException>(() => tempo.WithDataBindMount(null!));
        Assert.Throws<ArgumentException>(() => tempo.WithDataBindMount(string.Empty));
        Assert.Throws<ArgumentNullException>(() => tempo.WithConfigFile(null!));
        Assert.Throws<ArgumentException>(() => tempo.WithConfigFile(string.Empty));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishPreservesBindingsAndOptionalStorage(bool persistData)
    {
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        var tempo = builder.AddTempo("tempo");
        if (persistData)
        {
            tempo.WithDataVolume("tempo-data");
        }
        using var app = builder.Build();
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            var context = new ManifestPublishingContext(builder.ExecutionContext, Path.Combine(builder.AppHostDirectory, "manifest.json"), writer, default);
            await context.WriteContainerAsync(tempo.Resource);
            writer.WriteEndObject();
        }
        using var manifest = JsonDocument.Parse(output.ToArray());
        var resource = manifest.RootElement;
        Assert.Equal("container.v0", resource.GetProperty("type").GetString());
        Assert.Equal($"{TempoContainerImageTags.Registry}/{TempoContainerImageTags.Image}:{TempoContainerImageTags.Tag}", resource.GetProperty("image").GetString());
        Assert.False(resource.TryGetProperty("connectionString", out _));
        foreach (var endpoint in tempo.Resource.Annotations.OfType<EndpointAnnotation>())
        {
            var binding = resource.GetProperty("bindings").GetProperty(endpoint.Name);
            Assert.Equal(endpoint.TargetPort, binding.GetProperty("targetPort").GetInt32());
            Assert.Equal(endpoint.UriScheme, binding.GetProperty("scheme").GetString());
            Assert.Equal(endpoint.Transport, binding.GetProperty("transport").GetString());
        }
        if (persistData)
        {
            var volume = Assert.Single(resource.GetProperty("volumes").EnumerateArray());
            Assert.Equal("/var/tempo", volume.GetProperty("target").GetString());
            Assert.False(volume.GetProperty("readOnly").GetBoolean());
        }
        else
        {
            Assert.False(resource.TryGetProperty("volumes", out _));
        }
    }
}