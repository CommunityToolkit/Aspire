// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;
using Xunit;

namespace CommunityToolkit.Aspire.Marten.Tests;

public class MartenClientPublicApiTests
{
    [Fact]
    public void AddMartenClientShouldThrowWhenBuilderIsNull()
    {
        IHostApplicationBuilder builder = null!;

        var connectionName = "postgres";

        var action = () => builder.AddMartenClient(connectionName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddMartenClientShouldThrowWhenNameIsNull()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string connectionName = null!;

        var action = () => builder.AddMartenClient(connectionName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(connectionName), exception.ParamName);
    }

    [Fact]
    public void AddMartenClientShouldThrowWhenNameIsEmpty()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string connectionName = "";

        var action = () => builder.AddMartenClient(connectionName);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(connectionName), exception.ParamName);
    }

    [Fact]
    public void AddKeyedMartenClientShouldThrowWhenBuilderIsNull()
    {
        IHostApplicationBuilder builder = null!;

        var connectionName = "postgres";

        var action = () => builder.AddKeyedMartenClient(connectionName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddKeyedMartenClientShouldThrowWhenNameIsNull()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string name = null!;

        var action = () => builder.AddKeyedMartenClient(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddKeyedMartenClientShouldThrowWhenNameIsEmpty()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string name = "";

        var action = () => builder.AddKeyedMartenClient(name);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
}
