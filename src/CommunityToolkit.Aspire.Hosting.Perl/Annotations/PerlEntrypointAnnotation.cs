using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl; 

namespace CommunityToolkit.Aspire.Hosting.Perl.Annotations;

/// <summary>
/// An annotation that wires into a specific perlbrew environment to use for the given resource.
/// See documentation on perlbrew here: https://perlbrew.pl/
/// </summary>
/// <see cref="EntrypointType"/>
internal class PerlEntrypointAnnotation : IResourceAnnotation
{
    public required EntrypointType Type { get; set; }
    public required string Entrypoint { get; set; }
}