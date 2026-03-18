using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlAppResourceShapeTests
{
    [Fact]
    public void PerlAppResourceInheritsFromExecutableResource()
    {
        var resource = new PerlAppResource("test", "perl", "scripts");

        Assert.IsAssignableFrom<ExecutableResource>(resource);
    }

    [Fact]
    public void PerlAppResourceImplementsIResourceWithServiceDiscovery()
    {
        var resource = new PerlAppResource("test", "perl", "scripts");

        Assert.IsAssignableFrom<IResourceWithServiceDiscovery>(resource);
    }

    [Fact]
    public void PerlAppResourceSetsNameCorrectly()
    {
        var resource = new PerlAppResource("my-perl-app", "perl", "scripts");

        Assert.Equal("my-perl-app", resource.Name);
    }

    [Fact]
    public void PerlAppResourceSetsCommandCorrectly()
    {
        var resource = new PerlAppResource("test", "perl", "scripts");

        Assert.Equal("perl", resource.Command);
    }

    [Fact]
    public void PerlAppResourceSetsWorkingDirectoryCorrectly()
    {
        var resource = new PerlAppResource("test", "perl", "/path/to/scripts");

        Assert.Equal("/path/to/scripts", resource.WorkingDirectory);
    }
}
