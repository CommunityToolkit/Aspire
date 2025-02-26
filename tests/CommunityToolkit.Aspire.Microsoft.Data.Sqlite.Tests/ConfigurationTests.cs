using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Microsoft.Data.Sqlite.Tests;

public class ConfigurationTests
{
    [Fact]
    public void ConnectionStringIsNullByDefault() =>
        Assert.Null(new SqliteConnectionSettings().ConnectionString);

    [Fact]
    public void HealthChecksEnabledByDefault() =>
        Assert.False(new SqliteConnectionSettings().DisableHealthChecks);

    [Fact]
    public void ExtensionsIsEmptyByDefault() =>
        Assert.Empty(new SqliteConnectionSettings().Extensions);
}
