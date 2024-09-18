using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Ollama.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void VerifyDefaultModel()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<OllamaResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("ollama", resource.Name);

        Assert.Equal("llama3", resource.ModelName);
    }

    [Fact]
    public void VerifyCustomModel()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama", modelName: "custom");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<OllamaResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("ollama", resource.Name);

        Assert.Equal("custom", resource.ModelName);
    }

    [Fact]
    public void VerifyDefaultPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<OllamaResource>().SingleOrDefault();

        Assert.NotNull(resource);

        var httpEndpoint = resource.GetEndpoint("ollama");

        Assert.Equal(11434, httpEndpoint.TargetPort);
    }

    [Fact]
    public void VerifyCustomPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama", port: 12345);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<OllamaResource>().SingleOrDefault();

        Assert.NotNull(resource);

        var httpEndpoint = resource.GetEndpoint("ollama");

        Assert.Equal(12345, httpEndpoint.Port);
    }
}