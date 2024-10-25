namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Static Web Apps resource.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SwaResource"/> class.
/// </remarks>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory for the resource.</param>
public class SwaResource(string name, string workingDirectory) : ExecutableResource(name, "swa", workingDirectory)
{
}
