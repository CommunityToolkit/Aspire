using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Python.Extensions.Tests;

#pragma warning disable CS0612
public class ResourceCreationTests
{
    [Fact(Skip = "Being removed with https://github.com/CommunityToolkit/Aspire/issues/917")]
    public void DefaultUvicornApp()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddUvicornApp("uvicornapp", "../../examples/python/uvicornapp-api", "main:app");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<UvicornAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("uvicorn", resource.Command);
        Assert.Equal(NormalizePathForCurrentPlatform("../../examples/python/uvicornapp-api"), resource.WorkingDirectory);
    }

    [Fact]
    public void DefaultUvApp()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddUvApp("uvapp", "../../examples/python/uv-api", "uv-api");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<UvAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("uv", resource.Command);
        Assert.Equal(NormalizePathForCurrentPlatform("../../examples/python/uv-api"), resource.WorkingDirectory);
    }

    [Fact]
    public void DefaultStreamlitApp()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddStreamlitApp("streamlitapp", "../../examples/python/streamlit-api", "app.py");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<StreamlitAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("streamlit", resource.Command);
        Assert.Equal(NormalizePathForCurrentPlatform("../../examples/python/streamlit-api"), resource.WorkingDirectory);
    }

    static string NormalizePathForCurrentPlatform(string path)
    {
        if (string.IsNullOrWhiteSpace(path) == true)
        {
            return path;
        }

        // Fix slashes
        path = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        return Path.GetFullPath(path);
    }
}