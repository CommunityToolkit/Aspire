using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

internal class TestPackage : IPackageMetadata
{
    public static readonly string PackageBasePath = Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "", ".nuget", "packages", "microsoft.sqlserver.dacpacs.master", "160.2.3");

    public string PackageId { get; } = "Microsoft.SqlServer.Dacpacs.Master";

    public Version PackageVersion { get; } = new Version(160, 2, 3);

    public string PackagePath { get; } = PackageBasePath;
}