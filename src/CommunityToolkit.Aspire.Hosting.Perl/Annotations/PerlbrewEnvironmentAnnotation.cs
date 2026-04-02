using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Annotations;

/// <summary>
/// An annotation that wires into a specific perlbrew environment to use for the given resource.
/// See documentation on perlbrew here: https://perlbrew.pl/
/// </summary>
/// <param name="name">The perlbrew version name (e.g., <c>perl-5.38.0</c>).</param>
/// <param name="perlbrewPath">The command to access perlbrew, default of "perlbrew", which is standard.</param>
internal class PerlbrewEnvironmentAnnotation(string name, string? perlbrewPath) : IResourceAnnotation
{
    public string Name { get; set; } = name;
    public string PerlbrewPath { get; set; } = perlbrewPath ?? "perlbrew";

    /// <summary>
    /// The resolved perlbrew environment that provides executable path resolution.
    /// Set when <see cref="PerlAppResourceBuilderExtensions.WithPerlbrewEnvironment{T}"/> is called.
    /// </summary>
    public PerlbrewEnvironment? Environment { get; set; }
}