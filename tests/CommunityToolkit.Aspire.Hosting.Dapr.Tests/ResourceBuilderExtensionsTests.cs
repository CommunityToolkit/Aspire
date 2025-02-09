using Aspire.Hosting;
using Aspire.Hosting.Utils;
using System.Runtime.CompilerServices;

namespace CommunityToolkit.Aspire.Hosting.Dapr.Tests;
public class ResourceBuilderExtensionsTests
{
    [Fact]
    public void WithMetadataUsingStringAddsDaprComponentConfigurationAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var rb = builder.AddDaprPubSub("pubsub").WithMetadata("name", "value");
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());
        Assert.Single(resource.Annotations.OfType<DaprComponentConfigurationAnnotation>());
    }

    [Fact]
    public void WithMetadataUsingParameterResourceAddsDaprComponentConfigurationAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var parameter = builder.AddParameter("name", string.Empty);
        var rb = builder.AddDaprPubSub("pubsub").WithMetadata("name", parameter.Resource);
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());
        Assert.Single(resource.Annotations.OfType<DaprComponentConfigurationAnnotation>());
    }

    [Fact]
    public void WithMetadataUsingSecretParameterResourceAddsDaprComponentSecretAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var parameter = builder.AddParameter("secret", string.Empty, secret: true);
        var rb = builder.AddDaprPubSub("pubsub").WithMetadata("name", parameter.Resource);
        var resource = Assert.Single(builder.Resources.OfType<DaprComponentResource>());
        Assert.Single(resource.Annotations.OfType<DaprComponentSecretAnnotation>());
    }

}
