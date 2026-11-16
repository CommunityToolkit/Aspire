namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

internal static class SqlPolyglotAppHostTest
{
    private static readonly string[] s_msbuildEnvironmentVariables =
    [
        "MSBUILD_EXE_PATH",
        "MSBuildExtensionsPath",
        "MSBuildSDKsPath"
    ];

    public static async Task Run(Func<Task> test)
    {
        // MSBuildLocator in other SQL tests configures in-process MSBuild; child dotnet builds
        // must select their own SDK instead of inheriting those paths.
        Dictionary<string, string?> originalValues = s_msbuildEnvironmentVariables.ToDictionary(
            name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (string name in s_msbuildEnvironmentVariables)
            {
                Environment.SetEnvironmentVariable(name, null);
            }

            await test();
        }
        finally
        {
            foreach ((string name, string? value) in originalValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
