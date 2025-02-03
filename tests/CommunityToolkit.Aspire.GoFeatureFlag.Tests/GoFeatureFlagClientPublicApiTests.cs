// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.GoFeatureFlag.Tests;

public class GoFeatureFlagClientPublicApiTests
{
    [Fact]
    public void AddGoFeatureFlagClientShouldThrowWhenBuilderIsNull()
    {
        IHostApplicationBuilder builder = null!;

        var connectionName = "goff";

        var action = () => builder.AddGoFeatureFlagClient(connectionName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddGoFeatureFlagClientShouldThrowWhenNameIsNull()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string connectionName = null!;

        var action = () => builder.AddGoFeatureFlagClient(connectionName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(connectionName), exception.ParamName);
    }

    [Fact]
    public void AddGoFeatureFlagClientShouldThrowWhenNameIsEmpty()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string connectionName = "";

        var action = () => builder.AddGoFeatureFlagClient(connectionName);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(connectionName), exception.ParamName);
    }

    [Fact]
    public void AddKeyedGoFeatureFlagClientShouldThrowWhenBuilderIsNull()
    {
        IHostApplicationBuilder builder = null!;

        var connectionName = "goff";

        var action = () => builder.AddKeyedGoFeatureFlagClient(connectionName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddKeyedGoFeatureFlagClientShouldThrowWhenNameIsNull()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string name = null!;

        var action = () => builder.AddKeyedGoFeatureFlagClient(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddKeyedGoFeatureFlagClientShouldThrowWhenNameIsEmpty()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string name = "";

        var action = () => builder.AddKeyedGoFeatureFlagClient(name);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
}
