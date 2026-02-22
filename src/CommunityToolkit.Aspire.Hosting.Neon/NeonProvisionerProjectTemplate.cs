using Aspire.Hosting.ApplicationModel;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Aspire.Hosting;

internal static class NeonProvisionerProjectTemplate
{
    private const string ProvisionerProjectFileName = "CommunityToolkit.Aspire.Neon.Provisioner.csproj";

    private static readonly object SyncLock = new();

    private static readonly (string ResourceName, string RelativePath)[] EmbeddedTemplateFiles =
    [
        ("NeonProvisionerTemplate/CommunityToolkit.Aspire.Neon.Provisioner.csproj", "CommunityToolkit.Aspire.Neon.Provisioner.csproj"),
        ("NeonProvisionerTemplate/Program.cs", "Program.cs"),
        ("NeonProvisionerTemplate/Shared/NeonApiClient.cs", "Shared/NeonApiClient.cs"),
        ("NeonProvisionerTemplate/Shared/NeonApiContracts.cs", "Shared/NeonApiContracts.cs"),
        ("NeonProvisionerTemplate/Shared/NeonProvisionerContracts.cs", "Shared/NeonProvisionerContracts.cs"),
        ("NeonProvisionerTemplate/Shared/NeonProvisionerEnvironmentVariables.cs", "Shared/NeonProvisionerEnvironmentVariables.cs"),
    ];

    internal static string EnsureProject(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        string appHostFingerprint = ComputeAppHostFingerprint(builder.AppHostDirectory);
        string templateRootPath = Path.Combine(
            ResolveAspireProvisionerCacheRoot(),
            appHostFingerprint,
            "neon-provisioner");
        string projectPath = Path.Combine(templateRootPath, ProvisionerProjectFileName);

        lock (SyncLock)
        {
            Directory.CreateDirectory(templateRootPath);
            MaterializeTemplate(templateRootPath);
        }

        return projectPath;
    }

    private static string ResolveAspireProvisionerCacheRoot()
    {
        string? aspireHome = Environment.GetEnvironmentVariable("ASPIRE_HOME");
        if (!string.IsNullOrWhiteSpace(aspireHome))
        {
            return Path.Combine(aspireHome, "cache", "neon");
        }

        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userHome))
        {
            return Path.Combine(userHome, ".aspire", "cache", "neon");
        }

        return Path.Combine(Path.GetTempPath(), "aspire", "cache", "neon");
    }

    private static void MaterializeTemplate(string templateRootPath)
    {
        Assembly assembly = typeof(NeonProvisionerProjectTemplate).Assembly;

        foreach ((string resourceName, string relativePath) in EmbeddedTemplateFiles)
        {
            using Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream is null)
            {
                throw new DistributedApplicationException(
                    $"Unable to load embedded Neon provisioner template resource '{resourceName}'.");
            }

            string targetPath = Path.Combine(templateRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string? targetDirectory = Path.GetDirectoryName(targetPath);
            if (targetDirectory is not null)
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (File.Exists(targetPath))
            {
                continue;
            }

            WriteTemplateFile(targetPath, resourceStream);
        }
    }

    private static void WriteTemplateFile(string targetPath, Stream resourceStream)
    {
        const int maxAttempts = 3;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            resourceStream.Position = 0;

            try
            {
                using FileStream output = new(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                resourceStream.CopyTo(output);
                return;
            }
            catch (IOException) when (File.Exists(targetPath))
            {
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(25 * attempt);
            }
        }

        throw new DistributedApplicationException(
            $"Unable to materialize Neon provisioner template file '{targetPath}' after multiple attempts.");
    }

    private static string ComputeAppHostFingerprint(string appHostDirectory)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(appHostDirectory);
        byte[] hashBytes = SHA256.HashData(inputBytes);
        string hash = Convert.ToHexString(hashBytes);

        return hash[..16].ToLowerInvariant();
    }

}
