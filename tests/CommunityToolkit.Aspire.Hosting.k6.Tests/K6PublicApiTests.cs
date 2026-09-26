// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

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
    public async Task WithScriptShouldOmitVusAndDurationFlagsWhenNull()
    {
        var builder = new DistributedApplicationBuilder([]);
        var resourceBuilder = builder.AddK6("k6");
    
        resourceBuilder.WithScript("scripts/main.js");
    
        Assert.Equal(["run", "--address", "0.0.0.0:6565", "scripts/main.js"], await GetArgsAsync(resourceBuilder));
    }
    
    [Fact]
    public async Task WithScriptShouldEmitEachFlagIndependently()
    {
        var builder = new DistributedApplicationBuilder([]);
        var resourceBuilder = builder.AddK6("k6");
    
        resourceBuilder.WithScript("scripts/main.js", duration: "1m");
    
        Assert.Equal(["run", "--address", "0.0.0.0:6565", "--duration", "1m", "scripts/main.js"], await GetArgsAsync(resourceBuilder));
    }
    
    [Fact]
    public async Task WithScriptShouldEmitVusAndDurationWhenSet()
    {
        var builder = new DistributedApplicationBuilder([]);
        var resourceBuilder = builder.AddK6("k6");
    
        resourceBuilder.WithScript("scripts/main.js", 5, "30s");
    
        Assert.Equal(["run", "--address", "0.0.0.0:6565", "--vus", "5", "--duration", "30s", "scripts/main.js"], await GetArgsAsync(resourceBuilder));
    }
    
    private static async Task<List<string>> GetArgsAsync(IResourceBuilder<K6Resource> resourceBuilder)
    {
        var args = new List<object>();
        var context = new CommandLineArgsCallbackContext(args, CancellationToken.None);
        foreach (var annotation in resourceBuilder.Resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }
        return [.. args.Cast<string>()];
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
