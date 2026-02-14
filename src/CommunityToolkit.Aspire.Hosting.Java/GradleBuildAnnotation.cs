using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

internal class GradleBuildAnnotation(GradleOptions gradleOptions) : IResourceAnnotation
{
    public GradleOptions GradleOptions { get; } = gradleOptions;
}
