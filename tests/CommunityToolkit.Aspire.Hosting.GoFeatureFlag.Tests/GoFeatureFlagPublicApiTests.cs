// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.GoFeatureFlag.Tests;

public class GoFeatureFlagPublicApiTests
{
    [Fact]
    public void AddGoFeatureFlagContainerShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "Goff";

        var action = () => builder.AddGoFeatureFlag(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddGoFeatureFlagContainerShouldThrowWhenNameIsNull()
    {
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        string name = null!;

        var action = () => builder.AddGoFeatureFlag(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
    
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithGoffBindMountShouldThrowWhenBuilderIsNull(bool useVolume)
    {
        IResourceBuilder<GoFeatureFlagResource> builder = null!;
    
        Func<IResourceBuilder<GoFeatureFlagResource>>? action = null;
    
        if (useVolume)
        {
            action = () => builder.WithDataVolume();
        }
        else
        {
            const string source = "/data";
    
            action = () => builder.WithGoffBindMount(source);
        }
    
        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }
    
    [Fact]
    public void WithGoffBindMountShouldThrowWhenSourceIsNull()
    {
        var builder = new DistributedApplicationBuilder([]);
        var resourceBuilder = builder.AddGoFeatureFlag("Goff");
    
        string source = null!;
    
        var action = () => resourceBuilder.WithGoffBindMount(source);
    
        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(source), exception.ParamName);
    }
    
    [Fact]
    public void CtorGoFeatureFlagResourceShouldThrowWhenNameIsNull()
    {
        const string name = null!;
    
        var action = () => new GoFeatureFlagResource(name!);
    
        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
    
    [Fact]
    public void WithLogLevelShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<GoFeatureFlagResource> builder = null!;

#pragma warning disable CTASPIRE002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var action = () => builder.WithLogLevel(LogLevel.Trace);
#pragma warning restore CTASPIRE002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }
}
