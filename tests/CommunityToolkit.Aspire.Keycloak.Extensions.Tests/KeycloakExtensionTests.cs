using Aspire.Hosting;
using Moq;

namespace CommunityToolkit.Aspire.Keycloak.Extensions.Tests;

public partial class KeycloakExtensionTests
{
    private readonly IDistributedApplicationBuilder _app;

    public KeycloakExtensionTests()
    {
        _app = DistributedApplication.CreateBuilder();
    }

    [Fact]
    public void WithPostgresDev_Should_Throw_If_Builder_Is_Null()
    {
        IDistributedApplicationBuilder builder = null!;

        var act = () => builder.AddKeycloak("testkeycloak")
            .WithPostgres(null!);

        var exeption = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("builder", exeption.ParamName);
    }

    [Fact]
    public void WithPostgresDev_Should_Throw_If_Database_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _app.AddKeycloak("testkeycloak")
                .WithPostgres(null!));
    }

    [Fact]
    public void WithPostgres_ExplicitCredentials_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            KeycloakPostgresExtension.WithPostgres(null!,
                new Mock<IResourceBuilder<PostgresDatabaseResource>>().Object,
                new Mock<IResourceBuilder<ParameterResource>>().Object,
                new Mock<IResourceBuilder<ParameterResource>>().Object));
    }
}