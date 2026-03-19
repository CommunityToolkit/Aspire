namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class EntrypointTypeCoverageTests
{
    [Theory]
    [InlineData(EntrypointType.Script)]
    [InlineData(EntrypointType.API)]
    [InlineData(EntrypointType.Module)]
    [InlineData(EntrypointType.Executable)]
    public void EntrypointTypeHasExpectedValue(EntrypointType value)
    {
        Assert.Contains(value, Enum.GetValues<EntrypointType>());
    }
}
