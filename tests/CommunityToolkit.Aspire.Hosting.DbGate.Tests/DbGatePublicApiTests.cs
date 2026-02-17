using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.DbGate.Tests;

public class DbGatePublicApiTests
{
    [Fact]
    public void AddDbGateContainerShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        var action = () => builder.AddDbGate();

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithDataShouldThrowWhenBuilderIsNull(bool useVolume)
    {
        IResourceBuilder<DbGateContainerResource> builder = null!;

        Func<IResourceBuilder<DbGateContainerResource>>? action = null;

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
        var resourceBuilder = builder.AddDbGate();

        string source = null!;

        var action = () => resourceBuilder.WithDataBindMount(source);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(source), exception.ParamName);
    }

    [Fact]
    public void WithHostPortShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<DbGateContainerResource> builder = null!;

        Func<IResourceBuilder<DbGateContainerResource>>? action = null;

        action = () => builder.WithHostPort(9090);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void SanitizeConnectionIdShouldThrowWhenResourceNameIsNull()
    {
        string resourceName = null!;

        var action = () => DbGateBuilderExtensions.SanitizeConnectionId(resourceName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(resourceName), exception.ParamName);
    }

    [Theory]
    [InlineData("mysql", "mysql")]
    [InlineData("mysql-db", "mysql_db")]
    [InlineData("my-sql-db", "my_sql_db")]
    [InlineData("mysql_db", "mysql_db")]
    [InlineData("mysql-", "mysql_")]
    [InlineData("-mysql", "_mysql")]
    [InlineData("--mysql--", "__mysql__")]
    [InlineData("", "")]
    public void SanitizeConnectionIdShouldReplaceHyphensWithUnderscores(string input, string expected)
    {
        var result = DbGateBuilderExtensions.SanitizeConnectionId(input);

        Assert.Equal(expected, result);
    }
}
