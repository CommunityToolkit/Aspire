namespace CommunityToolkit.Aspire.Hosting.DbGate.Tests;

public class AppHostModelTests
{
    [Fact]
    public async Task MongoDbResourcesUseKernelCompatibleImage()
    {
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CommunityToolkit_Aspire_Hosting_DbGate_AppHost>();

        var resources = builder.Resources.OfType<MongoDBServerResource>().OrderBy(resource => resource.Name).ToArray();
        Assert.Equal(["mongodb1", "mongodb2"], resources.Select(resource => resource.Name));

        foreach (var resource in resources)
        {
            var image = Assert.Single(resource.Annotations.OfType<ContainerImageAnnotation>());
            Assert.Equal("docker.io", image.Registry);
            Assert.Equal("library/mongo", image.Image);
            Assert.Equal("7.0", image.Tag);
        }
    }
}
