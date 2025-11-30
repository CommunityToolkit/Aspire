using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Zitadel;

/// <summary>
/// Resource for the Zitadel API server.
/// </summary>
public sealed class ZitadelResource(string name) : ContainerResource(name)
{
    internal const string HttpEndpointName = "http";

    /// <summary>
    /// The parameter that contains the (default) Zitadel admin username.
    /// </summary>
    public ParameterResource? AdminUsernameParameter { get; }

    /// <summary>
    /// The parameter that contains the (default) Zitadel admin password.
    /// </summary>
    public ParameterResource AdminPasswordParameter { get; set; } = null!; // TODO fix null
}