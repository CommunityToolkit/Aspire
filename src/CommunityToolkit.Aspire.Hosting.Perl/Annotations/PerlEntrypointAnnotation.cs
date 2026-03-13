using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl; 

namespace CommunityToolkit.Aspire.Hosting.Perl.Annotations;

/// <summary>
/// An annotation that stores the entrypoint type and path for a Perl application resource,
/// used to determine how the application is launched (e.g., as a script or via a framework runner).
/// </summary>
/// <see cref="EntrypointType"/>
internal class PerlEntrypointAnnotation : IResourceAnnotation
{
    public required EntrypointType Type { get; set; }
    public required string Entrypoint { get; set; }
}