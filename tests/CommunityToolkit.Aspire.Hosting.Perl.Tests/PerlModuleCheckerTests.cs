using CommunityToolkit.Aspire.Hosting.Perl.Services;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlModuleCheckerTests
{
    [LinuxOnlyFact]
    public async Task IsModuleInstalledAsync_UsesProvidedEnvironmentVariables()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-module-check-");

        try
        {
            var moduleDir = Directory.CreateDirectory(Path.Combine(tempDir.FullName, "My"));
            var modulePath = Path.Combine(moduleDir.FullName, "Test.pm");
            await File.WriteAllTextAsync(modulePath, "package My::Test;\n1;\n");

            var withoutEnv = await PerlModuleChecker.IsModuleInstalledAsync("perl", "My::Test");
            Assert.False(withoutEnv);

            Dictionary<string, object> environmentVariables = new()
            {
                ["PERL5LIB"] = tempDir.FullName
            };

            var withEnv = await PerlModuleChecker.IsModuleInstalledAsync("perl", "My::Test", environmentVariables);
            Assert.True(withEnv);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }
}
