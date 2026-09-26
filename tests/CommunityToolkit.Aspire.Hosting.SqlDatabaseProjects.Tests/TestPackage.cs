using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

internal class TestPackage : IPackageMetadata
{
    public static readonly string NuGetPackageCache = GetNuGetPackageCache();

    public string PackageId { get; } = "ErikEJ.Dacpac.Chinook";

    public Version PackageVersion { get; } = new Version(1, 0, 0);

    public string PackagePath { get; } = Path.Combine(NuGetPackageCache, "erikej.dacpac.chinook", "1.0.0");

    private static string GetNuGetPackageCache()
    {
        var packagesPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(packagesPath))
        {
            return packagesPath;
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "nuget.config")))
            {
                return Path.Combine(directory.FullName, ".nugetpackages");
            }
        }

        return Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty, ".nuget", "packages");
    }
}
