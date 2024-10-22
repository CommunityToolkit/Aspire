using Aspire.Components.Common.Tests;
using Testcontainers.PostgreSql;
using Xunit;

namespace CommunityToolkit.Aspire.Marten.Tests;

public sealed class PostgreSQLContainerFixture : IAsyncLifetime
{
    public PostgreSqlContainer? Container { get; private set; }

    public string GetConnectionString() => Container?.GetConnectionString() ??
        throw new InvalidOperationException("The test container was not initialized.");

    public async Task InitializeAsync()
    {
        if (RequiresDockerAttribute.IsSupported)
        {
            Container = new PostgreSqlBuilder()
                .WithImage($"{PostgresContainerImageTags.Registry}/{PostgresContainerImageTags.Image}:{PostgresContainerImageTags.Tag}")
                .Build();
            await Container.StartAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (Container is not null)
        {
            await Container.DisposeAsync();
        }
    }
}
