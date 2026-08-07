using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Floci;

/// <summary>
/// The Floci emulator endpoint, as unresolved <see cref="ReferenceExpression"/>s. Handed to each
/// provider's <c>WithReference</c> overload so it only has to name its own environment variables;
/// Aspire resolves the expressions per dependent (host network for projects, container network for
/// sibling containers).
/// </summary>
/// <param name="HostAndPort">The endpoint without a scheme, e.g. <c>localhost:4566</c>.</param>
/// <param name="Url">The endpoint as an absolute URL, e.g. <c>http://localhost:4566</c>.</param>
internal readonly record struct FlociEndpoint(
    ReferenceExpression HostAndPort,
    ReferenceExpression Url);
