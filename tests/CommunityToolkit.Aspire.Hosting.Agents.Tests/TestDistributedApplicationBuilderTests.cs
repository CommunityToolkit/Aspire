namespace CommunityToolkit.Aspire.Hosting.Agents.Tests;

public class TestDistributedApplicationBuilderTests
{
    [Theory]
    [InlineData(DistributedApplicationOperation.Run)]
    [InlineData(DistributedApplicationOperation.Publish)]
    public void CreateUsesSharedHarnessAppHostMetadata(DistributedApplicationOperation operation)
    {
        using var builder = TestDistributedApplicationBuilder.Create(operation);

        Assert.Equal(typeof(TestDistributedApplicationBuilder).Assembly, builder.AppHostAssembly);
        Assert.Equal(operation, builder.ExecutionContext.Operation);

        using var app = builder.Build();
        Assert.Empty(app.Services.GetRequiredService<DistributedApplicationModel>().Resources);
    }
}
