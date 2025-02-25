// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using CommunityToolkit.Aspire.Utils;

namespace CommunityToolkit.Aspire.Hosting.Rust.Tests;
public class AddRustAppTests
{
    [Fact]
    public async Task AddRustAppAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var workingDirectory = "../../examples/rust/actix_api";
        var rustApp = appBuilder.AddRustApp("rust-app", workingDirectory);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RustAppExecutableResource>());
        workingDirectory = Path.Combine(appBuilder.AppHostDirectory, workingDirectory).NormalizePathForCurrentPlatform();
        Assert.Equal("rust-app", resource.Name);
        Assert.Equal(workingDirectory, resource.WorkingDirectory);
        Assert.Equal("cargo", resource.Command);
        var args = await resource.GetArgumentValuesAsync();
        Assert.Collection(args,
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal(".", arg));
    }

    [Fact]
    public async Task AddRustAppWithArgsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var workingDirectory = "../../examples/rust/actix_api";
        var rustApp = appBuilder.AddRustApp("rust-app", workingDirectory, ["--verbose"]);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RustAppExecutableResource>());
        workingDirectory = Path.Combine(appBuilder.AppHostDirectory, workingDirectory).NormalizePathForCurrentPlatform();
        Assert.Equal("rust-app", resource.Name);
        Assert.Equal(workingDirectory, resource.WorkingDirectory);
        Assert.Equal("cargo", resource.Command);
        var args = await resource.GetArgumentValuesAsync();
        Assert.Collection(args,
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal(".", arg),
            arg => Assert.Equal("--verbose", arg));
    }
}
