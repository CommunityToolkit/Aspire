// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Rust.Tests;

public class BaconAppPublicApiTests
{
    [Fact]
    public void AddBaconAppShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "bacon-app";
        const string workingDirectory = "bacon_app";
        var action = () => builder.AddBaconApp(name, workingDirectory);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddBaconAppShouldThrowWhenNameIsNull()
    {
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        const string name = null!;
        const string workingDirectory = "bacon_app";

        var action = () => builder.AddBaconApp(name!, workingDirectory);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddBaconAppShouldThrowWhenWorkingDirectoryIsNull()
    {
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        const string name = "bacon-app";
        const string workingDirectory = null!;

        var action = () => builder.AddBaconApp(name, workingDirectory!);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(workingDirectory), exception.ParamName);
    }
}
