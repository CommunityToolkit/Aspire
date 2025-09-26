using Aspire.Hosting;
using Moq;

namespace CommunityToolkit.Aspire.Keycloak.Extensions.Tests;

public class KeycloakExtensionTests
{
    private static async Task<IDictionary<string, string>> GetEnv(IResourceBuilder<KeycloakResource> kc)
    {
        return await kc.Resource.GetEnvironmentVariableValuesAsync();
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
        var app = DistributedApplication.CreateBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            app.AddKeycloak("testkeycloak")
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

    [Fact]
    public async Task WithPostgres_Defaults_SetBasicVars()
    {
        var app = DistributedApplication.CreateBuilder();
        var pg = app.AddPostgres("pg");
        var db = pg.AddDatabase("keycloakdb");
        var kc = app.AddKeycloak("kc")
            .WithPostgres(db);

        var env = await GetEnv(kc);

        Assert.Equal("postgres", env["KC_DB"]);
        Assert.True(env.ContainsKey("KC_DB_URL"));
        var url = env["KC_DB_URL"];
        Assert.StartsWith("jdbc:postgresql://", url);
        Assert.EndsWith("/keycloakdb", url);
    }

    [Fact]
    public async Task WithPostgres_ExplicitParameters_AreUsed()
    {
        var app = DistributedApplication.CreateBuilder();

        var pg = app.AddPostgres("pg");
        var db = pg.AddDatabase("keycloakdb");
        var user = app.AddParameter("kc-user");
        var pass = app.AddParameter("kc-pass");

        var kc = app.AddKeycloak("kc")
            .WithPostgres(db, user, pass);

        var env = await GetEnv(kc);

        Assert.False(ReferenceEquals(user.Resource, env["KC_DB_USERNAME"]));
        Assert.False(ReferenceEquals(pass.Resource, env["KC_DB_PASSWORD"]));
        Assert.True(env.ContainsKey("KC_DB_USERNAME"));
        Assert.True(env.ContainsKey("KC_DB_PASSWORD"));
    }

    [Fact]
    public async Task WithPostgres_Uses_ServerParameters_When_Present()
    {
        var app = DistributedApplication.CreateBuilder();

        var pg = app.AddPostgres("pg");
        var username = app.AddParameter("pg-user");
        var pass = app.AddParameter("pg-pass");

        pg.WithUserName(username)
            .WithPassword(pass);

        var db = pg.AddDatabase("keycloakdb");

        var kc = app.AddKeycloak("kc")
            .WithPostgres(db);

        var env = await GetEnv(kc);
        Assert.NotEqual("postgres", env["KC_DB_USERNAME"].ToString());
        Assert.NotEqual("postgres", env["KC_DB_PASSWORD"].ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WithPostgres_XA_Flag_Set_When_Enabled(bool xaEnabled)
    {
        var app = DistributedApplication.CreateBuilder();

        var pg = app.AddPostgres("pg");
        var db = pg.AddDatabase("keycloakdb");

        var kc = app.AddKeycloak("kc")
            .WithPostgres(db, xaEnabled);

        var env = await GetEnv(kc);
        Assert.Equal(xaEnabled.ToString().ToLower(), env["KC_TRANSACTION_XA_ENABLED"]);
    }
}