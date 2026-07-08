using Aspire.Hosting;
using Aspire.Hosting.Utils;

namespace CommunityToolkit.Aspire.Hosting.Listmonk.Tests;

public class ListmonkPublicApiTests
{
    [Fact]
    public void AddListmonkShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "listmonk";

        var action = () => builder.AddListmonk(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddListmonkShouldThrowWhenNameIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        string name = null!;

        var action = () => builder.AddListmonk(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void WithUploadsBindMountShouldThrowWhenSourceIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var listmonk = builder.AddListmonk("listmonk");
        string source = null!;

        var action = () => listmonk.WithUploadsBindMount(source);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(source), exception.ParamName);
    }

    [Fact]
    public void WithAppAddressShouldThrowWhenAddressIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var listmonk = builder.AddListmonk("listmonk");
        string address = null!;

        var action = () => listmonk.WithAppAddress(address);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(address), exception.ParamName);
    }

    [Fact]
    public void WithReferenceShouldThrowWhenBuilderIsNull()
    {
        var appBuilder = TestDistributedApplicationBuilder.Create();
        IResourceBuilder<ListmonkResource> listmonk = null!;
        var database = appBuilder.AddPostgres("postgres").AddDatabase("listmonkdb");

        var action = () => listmonk.WithReference(database);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void WithReferenceShouldThrowWhenDatabaseIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var listmonk = builder.AddListmonk("listmonk");
        IResourceBuilder<PostgresDatabaseResource> database = null!;

        var action = () => listmonk.WithReference(database);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal("database", exception.ParamName);
    }

    [Fact]
    public void WithDatabaseSslModeShouldThrowWhenSslModeIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var listmonk = builder.AddListmonk("listmonk");
        string sslMode = null!;

        var action = () => listmonk.WithDatabaseSslMode(sslMode);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(sslMode), exception.ParamName);
    }

    [Fact]
    public void WithDatabaseMaxOpenConnectionsShouldThrowWhenMaxOpenIsNegative()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var listmonk = builder.AddListmonk("listmonk");

        var action = () => listmonk.WithDatabaseMaxOpenConnections(-1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Equal("maxOpen", exception.ParamName);
    }

    [Fact]
    public void WithDatabaseMaxIdleConnectionsShouldThrowWhenMaxIdleIsNegative()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var listmonk = builder.AddListmonk("listmonk");

        var action = () => listmonk.WithDatabaseMaxIdleConnections(-1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Equal("maxIdle", exception.ParamName);
    }

    [Fact]
    public void WithDatabaseMaxLifetimeShouldThrowWhenMaxLifetimeIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var listmonk = builder.AddListmonk("listmonk");
        string maxLifetime = null!;

        var action = () => listmonk.WithDatabaseMaxLifetime(maxLifetime);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(maxLifetime), exception.ParamName);
    }

    [Fact]
    public void WithDatabaseParametersShouldThrowWhenParametersIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var listmonk = builder.AddListmonk("listmonk");
        string parameters = null!;

        var action = () => listmonk.WithDatabaseParameters(parameters);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(parameters), exception.ParamName);
    }

    [Fact]
    public void WithTimeZoneShouldThrowWhenTimeZoneIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var listmonk = builder.AddListmonk("listmonk");
        string timeZone = null!;

        var action = () => listmonk.WithTimeZone(timeZone);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(timeZone), exception.ParamName);
    }

    [Fact]
    public void WithAdminUserShouldThrowWhenUsernameIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var listmonk = builder.AddListmonk("listmonk");
        string username = null!;

        var action = () => listmonk.WithAdminUser(username);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(username), exception.ParamName);
    }

    [Fact]
    public void WithAdminPasswordShouldThrowWhenPasswordIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var listmonk = builder.AddListmonk("listmonk");

        var action = () => listmonk.WithAdminPassword(null!);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal("password", exception.ParamName);
    }

    [Fact]
    public void WithUserIdShouldThrowWhenUserIdIsNegative()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var listmonk = builder.AddListmonk("listmonk");

        var action = () => listmonk.WithUserId(-1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Equal("userId", exception.ParamName);
    }

    [Fact]
    public void WithGroupIdShouldThrowWhenGroupIdIsNegative()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var listmonk = builder.AddListmonk("listmonk");

        var action = () => listmonk.WithGroupId(-1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Equal("groupId", exception.ParamName);
    }

    [Fact]
    public void CtorListmonkResourceShouldThrowWhenNameIsNull()
    {
        string name = null!;

        var action = () => new ListmonkResource(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
}
