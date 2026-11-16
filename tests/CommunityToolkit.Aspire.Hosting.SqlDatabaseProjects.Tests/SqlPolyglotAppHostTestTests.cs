namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

public class SqlPolyglotAppHostTestTests
{
    [Fact]
    public async Task RestoresMSBuildEnvironmentAfterFailure()
    {
        string[] variables = ["MSBUILD_EXE_PATH", "MSBuildExtensionsPath", "MSBuildSDKsPath"];
        Dictionary<string, string?> originalValues = variables.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (string variable in variables)
            {
                Environment.SetEnvironmentVariable(variable, "test-sdk-path");
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() => SqlPolyglotAppHostTest.Run(() =>
            {
                foreach (string variable in variables)
                {
                    Assert.Null(Environment.GetEnvironmentVariable(variable));
                }

                throw new InvalidOperationException();
            }));

            foreach (string variable in variables)
            {
                Assert.Equal("test-sdk-path", Environment.GetEnvironmentVariable(variable));
            }
        }
        finally
        {
            foreach ((string variable, string? value) in originalValues)
            {
                Environment.SetEnvironmentVariable(variable, value);
            }
        }
    }
}
