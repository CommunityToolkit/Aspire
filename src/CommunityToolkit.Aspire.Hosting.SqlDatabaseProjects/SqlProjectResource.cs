using Microsoft.Build.Evaluation;
using Microsoft.SqlServer.Dac;
using System.Reflection;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a SQL Server Database project resource.
/// </summary>
/// <param name="name">Name of the resource.</param>
public sealed class SqlProjectResource(string name) : Resource(name), IResourceWithWaitSupport, IResourceWithDacpac
{
    string IResourceWithDacpac.GetDacpacPath()
    {
        if (this.TryGetLastAnnotation<IProjectMetadata>(out var projectMetadata))
        {
            var projectPath = projectMetadata.ProjectPath;
            using var projectCollection = new ProjectCollection();

            var attr = GetType().Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            if (attr is not null)
                projectCollection.SetGlobalProperty("Configuration", attr.Configuration);

            var project = projectCollection.LoadProject(projectPath);

            // .sqlprojx has a SqlTargetPath property, so try that first
            var targetPath = project.GetPropertyValue("SqlTargetPath");
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                targetPath = project.GetPropertyValue("TargetPath");
            }

            return targetPath;
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
