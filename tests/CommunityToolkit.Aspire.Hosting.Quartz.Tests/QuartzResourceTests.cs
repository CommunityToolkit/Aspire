using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CommunityToolkit.Aspire.Hosting.Quartz.Tests;

public class QuartzResourceTests
{
    [Fact]
    public void AddQuartzAddsResourceToBuilder()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var postgres = builder.AddPostgres("postgres").AddDatabase("quartzdb");

        // Act
        var quartz = builder.AddQuartz("quartz", postgres);

        // Assert
        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = appModel.Resources.OfType<QuartzResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("quartz", resource.Name);
    }

    [Fact]
    public void QuartzResourceHasCorrectType()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var postgres = builder.AddPostgres("postgres").AddDatabase("quartzdb");

        // Act
        var quartz = builder.AddQuartz("quartz", postgres);

        // Assert
        Assert.IsAssignableFrom<IResourceBuilder<QuartzResource>>(quartz);
    }

    [Fact]
    public void QuartzResourceExposesConnectionString()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var postgres = builder.AddPostgres("postgres").AddDatabase("quartzdb");

        // Act
        var quartz = builder.AddQuartz("quartz", postgres);

        // Assert
        Assert.NotNull(quartz.Resource.ConnectionStringExpression);
    }

    [Fact]
    public void CanAddMultipleQuartzResources()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var postgres1 = builder.AddPostgres("postgres1").AddDatabase("quartzdb1");
        var postgres2 = builder.AddPostgres("postgres2").AddDatabase("quartzdb2");

        // Act
        var quartz1 = builder.AddQuartz("quartz1", postgres1);
        var quartz2 = builder.AddQuartz("quartz2", postgres2);

        // Assert
        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resources = appModel.Resources.OfType<QuartzResource>().ToList();

        Assert.Equal(2, resources.Count);
        Assert.Contains(resources, r => r.Name == "quartz1");
        Assert.Contains(resources, r => r.Name == "quartz2");
    }
}
