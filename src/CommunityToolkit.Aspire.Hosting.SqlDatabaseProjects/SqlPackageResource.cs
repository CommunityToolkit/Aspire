using Microsoft.SqlServer.Dac;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a SQL Server Database package resource.
/// </summary>
/// <typeparam name="TPackage">Type that represents the package that contains the .dacpac file.</typeparam>
/// <param name="name">Name of the resource.</param>
public sealed class SqlPackageResource<TPackage>(string name) : Resource(name), IResourceWithWaitSupport, IResourceWithDacpac
    where TPackage : IPackageMetadata
{
    string IResourceWithDacpac.GetDacpacPath()
    {
        if (this.TryGetLastAnnotation<IPackageMetadata>(out var packageMetadata))
        {
            var packagePath = packageMetadata.PackagePath;
            if (this.TryGetLastAnnotation<DacpacMetadataAnnotation>(out var relativeDacpacMetadata))
            {
                return Path.Combine(packagePath, relativeDacpacMetadata.DacpacPath);
            }
            else
            {
                return Path.Combine(packagePath, "tools", packageMetadata.PackageId + ".dacpac");
            }
        }

        if (this.TryGetLastAnnotation<DacpacMetadataAnnotation>(out var dacpacMetadata))
        {
            return dacpacMetadata.DacpacPath;
        }

        throw new InvalidOperationException($"Unable to locate SQL Server Database project package for resource {Name}.");
    }

    DacDeployOptions IResourceWithDacpac.GetDacpacDeployOptions()
    {
        var options = new DacDeployOptions();

        if (this.TryGetLastAnnotation<ConfigureDacDeployOptionsAnnotation>(out var configureAnnotation))
        {
            configureAnnotation.ConfigureDeploymentOptions(options);
        }

        return options;
    }
}