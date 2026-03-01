namespace Aspire.Hosting.ApplicationModel;

internal sealed class NeonProvisionerExecutableResource(
    string name,
    string workingDirectory)
    : ExecutableResource(name, "dotnet", workingDirectory);
