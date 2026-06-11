using Aspire.Quartz;
using Xunit;

namespace CommunityToolkit.Aspire.Quartz.Tests;

public class QuartzClientTests
{
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
        Assert.Equal(BackoffStrategy.Exponential, policy.Strategy);
        Assert.Equal(TimeSpan.FromSeconds(5), policy.InitialDelay);
        Assert.Equal(2.0, policy.BackoffMultiplier);
        Assert.Equal(TimeSpan.FromMinutes(30), policy.MaxDelay);
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
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromMinutes(5)
        };

        // Assert
        Assert.Equal(5, policy.MaxAttempts);
        Assert.Equal(BackoffStrategy.Exponential, policy.Strategy);
        Assert.Equal(TimeSpan.FromSeconds(2), policy.InitialDelay);
        Assert.Equal(2.0, policy.BackoffMultiplier);
        Assert.Equal(TimeSpan.FromMinutes(5), policy.MaxDelay);
    }

    [Fact]
    public void JobContextCanBeCreated()
    {
        // Act
        var context = new JobContext
        {
            JobId = "test-job-123",
            JobType = "TestJob",
            Parameters = new Dictionary<string, object>(),
            RetryAttempt = 1,
            ScheduledTime = DateTimeOffset.UtcNow,
            StartTime = DateTimeOffset.UtcNow
        };

        // Assert
        Assert.Equal("test-job-123", context.JobId);
        Assert.Equal("TestJob", context.JobType);
        Assert.Equal(1, context.RetryAttempt);
    }

    [Fact]
    public void BackoffStrategyHasCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)BackoffStrategy.Linear);
        Assert.Equal(1, (int)BackoffStrategy.Exponential);
    }
}
