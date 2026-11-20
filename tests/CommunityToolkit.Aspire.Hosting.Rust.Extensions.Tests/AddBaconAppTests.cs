// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Rust.Extensions.Tests;
public class AddBaconAppTests
{
    [Fact]
    public async Task AddBaconAppAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var workingDirectory = "../../examples/rust/actix_api";
        var baconApp = appBuilder.AddBaconApp("bacon-app", workingDirectory);

        await using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RustAppExecutableResource>());
        workingDirectory = Path.Combine(appBuilder.AppHostDirectory, workingDirectory).NormalizePathForCurrentPlatform();
        Assert.Equal("bacon-app", resource.Name);
        Assert.Equal(workingDirectory, resource.WorkingDirectory);
        Assert.Equal("bacon", resource.Command);
        var args = await resource.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("run", arg));
    }

    [Fact]
    public async Task AddBaconAppWithArgsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var workingDirectory = "../../examples/rust/actix_api";
        var baconApp = appBuilder.AddBaconApp("bacon-app", workingDirectory, ["check"]);

        await using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RustAppExecutableResource>());
        workingDirectory = Path.Combine(appBuilder.AppHostDirectory, workingDirectory).NormalizePathForCurrentPlatform();
        Assert.Equal("bacon-app", resource.Name);
        Assert.Equal(workingDirectory, resource.WorkingDirectory);
        Assert.Equal("bacon", resource.Command);
        var args = await resource.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("check", arg));
    }

    [Fact]
    public async Task BaconAndUpstreamRustAppsCanCoexist()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var rust = appBuilder.AddRustApp("rust-app", "../../examples/rust/actix_api");
        var bacon = appBuilder.AddBaconApp("bacon-app", "../../examples/rust/actix_api", []);

        await using var app = appBuilder.Build();

        Assert.Equal("cargo", rust.Resource.Command);
        Assert.Equal("bacon", bacon.Resource.Command);
        Assert.Equal(["run"], await bacon.Resource.GetArgumentListAsync());
    }

    [Fact]
    public async Task BaconRetainsStableBuilderTypeAndSupportsReferencesAndDockerfilePublishing()
    {
        var appBuilder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        IResourceBuilder<RustAppExecutableResource> bacon = appBuilder
            .AddBaconApp("bacon-app", "../../examples/rust/actix_api")
            .WithHttpEndpoint(env: "PORT");
        appBuilder.AddExecutable("consumer", "consumer", ".").WithReference(bacon);

        await using var app = appBuilder.Build();

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var publishedResource = Assert.IsAssignableFrom<ContainerResource>(
            Assert.Single(model.Resources, resource => resource.Name == bacon.Resource.Name));
        Assert.True(publishedResource.TryGetLastAnnotation<DockerfileBuildAnnotation>(out _));
        Assert.Contains(publishedResource.Annotations.OfType<EndpointAnnotation>(), endpoint => endpoint.Name == "http");
    }
}
