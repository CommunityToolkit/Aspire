using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Python.Extensions.Tests;

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
        Assert.Equal(NormalizePathForCurrentPlatform("../../examples/uvicorn/uvicornapp-api"), resource.WorkingDirectory);
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