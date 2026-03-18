using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlModuleInstallerResourceShapeTests
{
    [Fact]
    public void PerlModuleInstallerResourceInheritsFromExecutableResource()
    {
        var resource = new PerlModuleInstallerResource("installer", "cpanm", "scripts");

        Assert.IsAssignableFrom<ExecutableResource>(resource);
    }

    [Fact]
    public void PerlModuleInstallerResourceSetsNameCorrectly()
    {
        var resource = new PerlModuleInstallerResource("Mojolicious-installer", "cpanm", "scripts");

        Assert.Equal("Mojolicious-installer", resource.Name);
    }
}
