using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Annotations;

/// <summary>
/// An annotation for noting that a specific module is required.  
/// 
/// It also contains whether Force command should be used to install.
/// </summary>
internal class PerlRequiredModuleAnnotation : IResourceAnnotation
{
    public string Name { get; set; } = string.Empty;
    public bool Force { get; set; }
    public bool SkipTest { get; set; }
}
