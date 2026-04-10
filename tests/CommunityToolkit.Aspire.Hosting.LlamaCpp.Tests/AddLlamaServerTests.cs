using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
namespace CommunityToolkit.Aspire.Hosting.LlamaCpp.Tests;

public class AddLlamaServerTests
{
    const string modelUrl = "this.is/some-model";
    [Fact]
    public void AddLlamaServer_WithModelUrl_SetsModelName()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddLlamaServer("my-llama-server", modelUrl);
        var app = builder.Build();
        var resource = app.GetResource<LlamaCppServerResource>("my-llama-server");
        Assert.NotNull(resource);
        Assert.Equal(modelUrl, resource.ModelName);
    }
}
