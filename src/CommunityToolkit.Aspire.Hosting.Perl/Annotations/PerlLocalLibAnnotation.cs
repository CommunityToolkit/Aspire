using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Annotations;

internal sealed class PerlLocalLibAnnotation(string path) : IResourceAnnotation
{
    public string Path { get; } = path;
}
