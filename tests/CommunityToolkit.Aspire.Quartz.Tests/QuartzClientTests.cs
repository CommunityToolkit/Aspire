using Aspire.Quartz;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CommunityToolkit.Aspire.Quartz.Tests;

public class QuartzClientTests
{
    [Fact]
    public void AddQuartzClientRegistersServices()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:quartzdb"] = "Host=localhost;Database=quartz;";

        // Act
        builder.Services.AddQuartzClient(builder.Configuration.GetConnectionString("quartzdb")!);

        // Assert
        using var host = builder.Build();
        var jobClient = host.Services.GetService<IBackgroundJobClient>();

        Assert.NotNull(jobClient);
    }

    [Fact]
    public void AddQuartzClientThrowsWhenConnectionStringIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddQuartzClient(null!));
    }

    [Fact]
    public void AddQuartzClientThrowsWhenConnectionStringIsEmpty()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => services.AddQuartzClient(string.Empty));
    }

    [Fact]
    public void JobOptionsCanBeCreatedWithDefaults()
    {
        // Act
        var options = new JobOptions();

        // Assert
        Assert.Null(options.IdempotencyKey);
        Assert.Null(options.RetryPolicy);
    }

    [Fact]
    public void JobOptionsCanSetIdempotencyKey()
    {
        // Act
        var options = new JobOptions
        {
            IdempotencyKey = "test-key"
        };

        // Assert
        Assert.Equal("test-key", options.IdempotencyKey);
    }

    [Fact]
    public void RetryPolicyCanBeCreatedWithDefaults()
    {
        // Act
        var policy = new RetryPolicy();

        // Assert
        Assert.Equal(3, policy.MaxAttempts);
        Assert.Equal(BackoffStrategy.Linear, policy.Strategy);
    }

    [Fact]
    public void RetryPolicyCanSetCustomValues()
    {
        // Act
        var policy = new RetryPolicy
        {
            MaxAttempts = 5,
            Strategy = BackoffStrategy.Exponential,
            InitialDelay = TimeSpan.FromSeconds(2),
            Multiplier = 2.0,
            MaxDelay = TimeSpan.FromMinutes(5)
        };

        // Assert
        Assert.Equal(5, policy.MaxAttempts);
        Assert.Equal(BackoffStrategy.Exponential, policy.Strategy);
        Assert.Equal(TimeSpan.FromSeconds(2), policy.InitialDelay);
        Assert.Equal(2.0, policy.Multiplier);
        Assert.Equal(TimeSpan.FromMinutes(5), policy.MaxDelay);
    }
}
