// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.EventStore.Tests;

public class EventStorePublicApiTests
{
    [Fact]
    public void AddEventStoreShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "eventstore";

        var action = () => builder.AddEventStore(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddEventStoreShouldThrowWhenNameIsNull()
    {
        var builder = new DistributedApplicationBuilder([]);
        string name = null!;

        var action = () => builder.AddEventStore(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithDataShouldThrowWhenBuilderIsNull(bool useVolume)
    {
        IResourceBuilder<EventStoreResource> builder = null!;

        Func<IResourceBuilder<EventStoreResource>>? action = null;

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
        var eventstore = builder.AddEventStore("eventstore");

        string source = null!;

        var action = () => eventstore.WithDataBindMount(source);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(source), exception.ParamName);
    }

    [Fact]
    public void WithDataVolumeShouldAddMountAnnotation()
    {
        var builder = new DistributedApplicationBuilder([]);
        var eventstore = builder.AddEventStore("eventstore")
                                .WithDataVolume(name: null);
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<EventStoreResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("eventstore", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotation));
        Assert.EndsWith("-data", mountAnnotation.Source);
        Assert.Equal("/var/lib/eventstore", mountAnnotation.Target);
    }

    [Fact]
    public void WithNamedDataVolumeShouldAddMountAnnotation()
    {
        var builder = new DistributedApplicationBuilder([]);
        var eventstore = builder.AddEventStore("eventstore")
                                .WithDataVolume("mydata");
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<EventStoreResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("eventstore", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotation));
        Assert.Equal("mydata", mountAnnotation.Source);
        Assert.Equal("/var/lib/eventstore", mountAnnotation.Target);
    }

    [Fact]
    public void WithDataBindMountShouldAddMountAnnotation()
    {
        var builder = new DistributedApplicationBuilder([]);
        var eventstore = builder.AddEventStore("eventstore")
                                .WithDataBindMount("./mydata");
        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<EventStoreResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("eventstore", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerMountAnnotation? mountAnnotation));
        Assert.EndsWith("mydata", mountAnnotation.Source);
        Assert.Equal("/var/lib/eventstore", mountAnnotation.Target);
    }

    [Fact]
    public void EventStoreResourceCtorShouldThrowWhenNameIsNull()
    {
        var builder = new DistributedApplicationBuilder([]);
        const string name = null!;

        var action = () => new EventStoreResource(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
}
