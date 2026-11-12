using System.Text.Json;
using CommunityToolkit.Aspire.Testing;
using Xunit.Sdk;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

internal static class SqlPolyglotRestoreDiagnostics
{
    public static async Task Run(string appHostProject, Func<Task> test)
    {
        try
        {
            await test();
        }
        catch (XunitException original)
        {
            string appHostDirectory = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "..", "examples", "sql-database-projects", appHostProject));
            string integrationDirectory = Path.Combine(appHostDirectory, ".aspire", "integrations", "apphosts");
            if (!Directory.Exists(integrationDirectory))
            {
                throw;
            }

            string? project = Directory.EnumerateFiles(integrationDirectory, "IntegrationRestore.csproj", SearchOption.AllDirectories).FirstOrDefault();
            if (project is null)
            {
                throw;
            }

            using JsonDocument config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(appHostDirectory, "aspire.config.json")));
            string? version = config.RootElement.GetProperty("sdk").GetProperty("version").GetString();
            try
            {
                await ProcessTestUtilities.RunProcessAsync(
                    "dotnet",
                    ["build", project, $"-p:AspireIntegrationHostingVersion={version}"],
                    appHostDirectory,
                    TestContext.Current.CancellationToken);
            }
            catch (XunitException diagnostic)
            {
                throw new XunitException($"Original test failure:{Environment.NewLine}{original.Message}{Environment.NewLine}Generated integration build diagnostics:{Environment.NewLine}{diagnostic.Message}");
            }

            throw;
        }
    }
}
