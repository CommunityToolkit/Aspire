namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Static Web Apps resource.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SwaResource"/> class.
/// </remarks>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory for the resource.</param>
[Obsolete(
    message: "The SWA emulator integration is going to be removed in a future release.",
    error: false,
    DiagnosticId = "CTASPIRE003",
    UrlFormat = "https://github.com/CommunityToolit/aspire/issues/698")]
public class SwaResource(string name, string workingDirectory) : ExecutableResource(name, "swa", workingDirectory)
{
}
