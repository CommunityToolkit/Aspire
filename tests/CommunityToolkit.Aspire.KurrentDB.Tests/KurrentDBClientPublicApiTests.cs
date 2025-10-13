// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;
using Xunit;

namespace CommunityToolkit.Aspire.KurrentDB.Tests;

public class KurrentDBClientPublicApiTests
{
    [Fact]
    public void AddKurrentDBClientShouldThrowWhenBuilderIsNull()
    {
        IHostApplicationBuilder builder = null!;

        var connectionName = "kurrentdb";

        var action = () => builder.AddKurrentDBClient(connectionName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddKurrentDBClientShouldThrowWhenNameIsNull()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string connectionName = null!;

        var action = () => builder.AddKurrentDBClient(connectionName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(connectionName), exception.ParamName);
    }

    [Fact]
    public void AddKurrentDBClientShouldThrowWhenNameIsEmpty()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string connectionName = "";

        var action = () => builder.AddKurrentDBClient(connectionName);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(connectionName), exception.ParamName);
    }

    [Fact]
    public void AddKeyedKurrentDBClientShouldThrowWhenBuilderIsNull()
    {
        IHostApplicationBuilder builder = null!;

        var connectionName = "kurrentdb";

        var action = () => builder.AddKeyedKurrentDBClient(connectionName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddKeyedKurrentDBClientShouldThrowWhenNameIsNull()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string name = null!;

        var action = () => builder.AddKeyedKurrentDBClient(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddKeyedKurrentDBClientShouldThrowWhenNameIsEmpty()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string name = "";

        var action = () => builder.AddKeyedKurrentDBClient(name);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
}
