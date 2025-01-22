using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

internal class MavenBuildAnnotation(MavenOptions mavenOptions) : IResourceAnnotation
{
    public MavenOptions MavenOptions { get; } = mavenOptions;
}