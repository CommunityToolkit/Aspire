namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class EntrypointTypeCoverageTests
{
    [Fact]
    public void EntrypointTypeHasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(EntrypointType), EntrypointType.Script));
        Assert.True(Enum.IsDefined(typeof(EntrypointType), EntrypointType.API));
        Assert.True(Enum.IsDefined(typeof(EntrypointType), EntrypointType.Module));
        Assert.True(Enum.IsDefined(typeof(EntrypointType), EntrypointType.Executable));
    }
}
