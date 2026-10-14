using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Agents.Tests;

internal static class TestServiceProvider
{
    public static IServiceProvider Instance { get; } = new ServiceCollection()
        .AddSingleton<IConfiguration>(new ConfigurationManager())
        .BuildServiceProvider();
}
