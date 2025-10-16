namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a metadata annotation that specifies that .dacpac deployment should be skipped if metadata in the target database indicates that the .dacpac has already been deployed in it's current state.
/// </summary>
public sealed class DacpacSkipWhenDeployedAnnotation : IResourceAnnotation
{
}
