// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Sockets;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Rust.Tests;
public class AddRustAppTests
{
    [Fact]
    public void AddRustAppAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var rustApp = appBuilder.AddRustApp("rust-app", "../../examples/rust/actix_api");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<RustAppExecutableResource>());
        Assert.Equal("rust-app", containerResource.Name);
    }
}
