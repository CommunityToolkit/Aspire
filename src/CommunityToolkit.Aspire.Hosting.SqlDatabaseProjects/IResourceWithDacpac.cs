using Microsoft.SqlServer.Dac;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a resource that produces a .dacpac file.
/// </summary>
public interface IResourceWithDacpac : IResource, IResourceWithWaitSupport
{
    internal string GetDacpacPath();
    internal DacDeployOptions GetDacpacDeployOptions();
}