using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Python.Extensions.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void DefaultUvicornApp()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddUvicornApp("uvicornapp", "../../examples/uvicorn/uvicornapp-api", "main:app");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<UvicornAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("uvicorn", resource.Command);
        Assert.Equal(NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, "../../examples/uvicorn/uvicornapp-api")), resource.WorkingDirectory);
    }

    private string NormalizePathForCurrentPlatform(string path)
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