using Microsoft.SqlServer.Dac;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a metadata annotation that specifies dacpac deployment options.
/// </summary>
/// <param name="ConfigureDeploymentOptions">deployment options</param>
public record ConfigureDacDeployOptionsAnnotation(Action<DacDeployOptions> ConfigureDeploymentOptions) : IResourceAnnotation
{
}