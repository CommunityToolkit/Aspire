// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using CommunityToolkit.Aspire.Utils;
using CommunityToolkit.Aspire.Testing;

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
        var args = await resource.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("run", arg));
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
        var args = await resource.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("--verbose", arg));
    }

    [Fact]
    public async Task WithCargoCommandChangesCommandAndArgs()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var workingDirectory = "../../examples/rust/actix_api";
        var rustApp = appBuilder.AddRustApp("rust-app", workingDirectory)
            .WithCargoCommand("trunk", "serve");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RustAppExecutableResource>());
        Assert.Equal("trunk", resource.Command);
        var args = await resource.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("serve", arg));
    }

    [Fact]
    public async Task WithCargoCommandChangesCommandWithAdditionalArgs()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var workingDirectory = "../../examples/rust/actix_api";
        var rustApp = appBuilder.AddRustApp("rust-app", workingDirectory)
            .WithCargoCommand("cargo-leptos", "watch", "--hot-reload");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RustAppExecutableResource>());
        Assert.Equal("cargo-leptos", resource.Command);
        var args = await resource.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("watch", arg),
            arg => Assert.Equal("--hot-reload", arg));
    }

    [Fact]
    public async Task WithCargoCommandNoArgs()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var workingDirectory = "../../examples/rust/actix_api";
        var rustApp = appBuilder.AddRustApp("rust-app", workingDirectory)
            .WithCargoCommand("my-tool");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RustAppExecutableResource>());
        Assert.Equal("my-tool", resource.Command);
        var args = await resource.GetArgumentListAsync();
        Assert.Empty(args);
    }

    [Fact]
    public async Task WithCargoInstallCreatesInstallerResource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var workingDirectory = "../../examples/rust/actix_api";
        var rustApp = appBuilder.AddRustApp("rust-app", workingDirectory)
            .WithCargoInstall("trunk");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var rustResource = Assert.Single(appModel.Resources.OfType<RustAppExecutableResource>());
        var installer = Assert.Single(appModel.Resources.OfType<RustToolInstallerResource>());
        Assert.Equal("rust-app-cargo-install-trunk", installer.Name);
        Assert.Equal("cargo", installer.Command);
        var args = await installer.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("install", arg),
            arg => Assert.Equal("trunk", arg));

        Assert.True(rustResource.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        Assert.Contains(waitAnnotations, w => w.Resource == installer);
    }

    [Fact]
    public async Task WithCargoInstallWithVersionAndLocked()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var workingDirectory = "../../examples/rust/actix_api";
        var rustApp = appBuilder.AddRustApp("rust-app", workingDirectory)
            .WithCargoInstall("cargo-leptos", version: "0.2.0", locked: true);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var installer = Assert.Single(appModel.Resources.OfType<RustToolInstallerResource>());
        var args = await installer.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("install", arg),
            arg => Assert.Equal("--version", arg),
            arg => Assert.Equal("0.2.0", arg),
            arg => Assert.Equal("--locked", arg),
            arg => Assert.Equal("cargo-leptos", arg));
    }

    [Fact]
    public async Task WithCargoInstallWithFeatures()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var workingDirectory = "../../examples/rust/actix_api";
        var rustApp = appBuilder.AddRustApp("rust-app", workingDirectory)
            .WithCargoInstall("cargo-leptos", features: ["ssr", "hydrate"]);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var installer = Assert.Single(appModel.Resources.OfType<RustToolInstallerResource>());
        var args = await installer.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("install", arg),
            arg => Assert.Equal("--features", arg),
            arg => Assert.Equal("ssr,hydrate", arg),
            arg => Assert.Equal("cargo-leptos", arg));
    }

    [Fact]
    public async Task WithCargoInstallWithBinstall()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var workingDirectory = "../../examples/rust/actix_api";
        var rustApp = appBuilder.AddRustApp("rust-app", workingDirectory)
            .WithCargoInstall("trunk", binstall: true);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var installer = Assert.Single(appModel.Resources.OfType<RustToolInstallerResource>());
        var args = await installer.GetArgumentListAsync();
        Assert.Collection(args,
            arg => Assert.Equal("binstall", arg),
            arg => Assert.Equal("-y", arg),
            arg => Assert.Equal("trunk", arg));
    }
}
