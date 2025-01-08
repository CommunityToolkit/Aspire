using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite.Tests;

public class ConfigurationTests
{
    [Fact]
    public void ConnectionStringIsNullByDefault() =>
        Assert.Null(new SqliteEntityFrameworkCoreSettings().ConnectionString);

    [Fact]
    public void HealthChecksEnabledByDefault() =>
        Assert.False(new SqliteEntityFrameworkCoreSettings().DisableHealthChecks);
}
