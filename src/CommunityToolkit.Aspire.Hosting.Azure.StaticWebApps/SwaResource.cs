using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps;

public class SwaResource(string name, string workingDirectory) : ExecutableResource(name, "swa", workingDirectory)
{
}
