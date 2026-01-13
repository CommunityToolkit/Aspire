namespace CommunityToolkit.Aspire.OllamaSharp.Tests;

public class ConfigurationTests
{
    [Fact]
    public void EndpointIsNullByDefault() =>
        Assert.Null(new OllamaSharpSettings().Endpoint);

    [Fact]
    public void HealthChecksEnabledByDefault() =>
        Assert.False(new OllamaSharpSettings().DisableHealthChecks);

    [Fact]
    public void SelectedModelIsNullByDefault() =>
        Assert.Null(new OllamaSharpSettings().SelectedModel);

    [Fact]
    public void ModelsIsEmptyByDefault() =>
        Assert.Empty(new OllamaSharpSettings().Models);

    [Fact]
    public void JsonSerializerContextIsNullByDefault() =>
        Assert.Null(new OllamaSharpSettings().JsonSerializerContext);
}
