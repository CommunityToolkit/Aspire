using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace CommunityToolkit.Aspire.Hosting.Deno.Tests;

public class ResourceCreationTests
{

    [Fact]
    public void DenoTaskUsesDenoCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddDenoTask("deno", Environment.CurrentDirectory);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<DenoAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("deno", resource.Command);
    }

    [Fact]
    public void DenoAppUsesDenoCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddDenoApp("deno", Environment.CurrentDirectory);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<DenoAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("deno", resource.Command);
    }
}