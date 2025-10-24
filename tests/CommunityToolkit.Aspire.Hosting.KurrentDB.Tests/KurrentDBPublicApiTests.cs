// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.KurrentDB.Tests;

public class KurrentDBPublicApiTests
{
    [Fact]
    public void AddKurrentDBShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "kurrentdb";

        var action = () => builder.AddKurrentDB(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddKurrentDBShouldThrowWhenNameIsNull()
    {
        var builder = new DistributedApplicationBuilder([]);
        string name = null!;

        var action = () => builder.AddKurrentDB(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithDataShouldThrowWhenBuilderIsNull(bool useVolume)
    {
        IResourceBuilder<KurrentDBResource> builder = null!;

        Func<IResourceBuilder<KurrentDBResource>>? action = null;

        if (useVolume)
        {
            action = () => builder.WithDataVolume();
        }
        else
        {
            const string source = "/data";

            action = () => builder.WithDataBindMount(source);
        }

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithDataBindMountShouldThrowWhenSourceIsNull()
    {
        var builder = new DistributedApplicationBuilder([]);
        var kurrentdb = builder.AddKurrentDB("kurrentdb");

        string source = null!;

        var action = () => kurrentdb.WithDataBindMount(source);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(source), exception.ParamName);
    }

    [Fact]
    public void WithDataVolumeShouldAddMountAnnotation()
    {
        var builder = new DistributedApplicationBuilder([]);
        var kurrentdb = builder.AddKurrentDB("kurrentdb")
                                .WithDataVolume(name: null);
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<KurrentDBResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("kurrentdb", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotation));
        Assert.EndsWith("-data", mountAnnotation.Source);
        Assert.Equal("/var/lib/kurrentdb", mountAnnotation.Target);
    }

    [Fact]
    public void WithNamedDataVolumeShouldAddMountAnnotation()
    {
        var builder = new DistributedApplicationBuilder([]);
        var kurrentdb = builder.AddKurrentDB("kurrentdb")
                                .WithDataVolume("mydata");
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<KurrentDBResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("kurrentdb", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotation));
        Assert.Equal("mydata", mountAnnotation.Source);
        Assert.Equal("/var/lib/kurrentdb", mountAnnotation.Target);
    }

    [Fact]
    public void WithDataBindMountShouldAddMountAnnotation()
    {
        var builder = new DistributedApplicationBuilder([]);
        var kurrentdb = builder.AddKurrentDB("kurrentdb")
                                .WithDataBindMount("./mydata");
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<KurrentDBResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("kurrentdb", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotation));
        Assert.EndsWith("mydata", mountAnnotation.Source);
        Assert.Equal("/var/lib/kurrentdb", mountAnnotation.Target);
    }

    [Fact]
    public void KurrentDBResourceCtorShouldThrowWhenNameIsNull()
    {
        var builder = new DistributedApplicationBuilder([]);
        const string name = null!;

        var action = () => new KurrentDBResource(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
}
