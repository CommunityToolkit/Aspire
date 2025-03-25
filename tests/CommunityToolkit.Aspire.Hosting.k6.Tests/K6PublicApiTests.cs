// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.k6.Tests;

public class K6PublicApiTests
{
    [Fact]
    public void AddK6ContainerShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "k6";

        var action = () => builder.AddK6(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddK6ContainerShouldThrowWhenNameIsNull()
    {
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        string name = null!;

        var action = () => builder.AddK6(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
    
    [Fact]
    public void WithScriptShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<K6Resource> builder = null!;
    
        const string scriptPath = "/scripts/main.js";
        
        var action = () => builder.WithScript(scriptPath);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }
    
    [Fact]
    public void WithScriptShouldThrowWhenScriptPathIsNull()
    {
        var builder = new DistributedApplicationBuilder([]);
        var resourceBuilder = builder.AddK6("k6");
    
        string scriptPath = null!;
    
        var action = () => resourceBuilder.WithScript(scriptPath);
    
        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(scriptPath), exception.ParamName);
    }
    
    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    public void WithScriptShouldThrowWhenVirtualUsersIsNegativeOrZero(int virtualUsers)
    {
        var builder = new DistributedApplicationBuilder([]);
        var resourceBuilder = builder.AddK6("k6");
    
        const string scriptPath = "scripts/main.js";
    
        var action = () => resourceBuilder.WithScript(scriptPath, virtualUsers);
    
        var exception = Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Equal(nameof(virtualUsers), exception.ParamName);
    }
    
    [Fact]
    public void WithScriptShouldThrowWhenDurationIsNull()
    {
        var builder = new DistributedApplicationBuilder([]);
        var resourceBuilder = builder.AddK6("k6");
    
        const string scriptPath = "scripts/main.js";
        int vus = 10;
        string duration = null!;
    
        var action = () => resourceBuilder.WithScript(scriptPath, vus, duration);
    
        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(duration), exception.ParamName);
    }
    
    [Fact]
    public void CtorK6ResourceShouldThrowWhenNameIsNull()
    {
        const string name = null!;
    
        var action = () => new K6Resource(name!);
    
        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
}
