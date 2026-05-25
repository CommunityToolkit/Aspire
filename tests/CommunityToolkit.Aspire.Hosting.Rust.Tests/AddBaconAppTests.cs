// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using CommunityToolkit.Aspire.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Rust.Tests;
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
}
