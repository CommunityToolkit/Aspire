#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a stable-diffusion.cpp HTTP server container.
/// </summary>
[AspireExport(ExposeProperties = true)]
public sealed class StableDiffusionCppResource(
    string name,
    StableDiffusionCppImageVariant imageVariant,
    string modelsDirectory,
    string outputDirectory)
    : ContainerResource(name), IResourceWithConnectionString
{
    internal const string HttpEndpointName = "http";
    internal const int HttpTargetPort = 1234;

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the selected official container image variant.
    /// </summary>
    public StableDiffusionCppImageVariant ImageVariant { get; } = imageVariant;

    internal string ModelsDirectory { get; } = modelsDirectory;

    internal string OutputDirectory { get; } = outputDirectory;

    internal ParameterResource? HuggingFaceToken { get; private set; }

    /// <summary>
    /// Gets the configured Hugging Face model.
    /// </summary>
    public StableDiffusionCppModelResource? Model { get; private set; }

    /// <summary>
    /// Gets the primary HTTP endpoint.
    /// </summary>
    public EndpointReference PrimaryEndpoint =>
        _primaryEndpoint ??= new EndpointReference(this, HttpEndpointName);

    /// <summary>
    /// Gets the HTTP server URI.
    /// </summary>
    public ReferenceExpression UriExpression =>
        ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

    /// <inheritdoc />
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"Endpoint={UriExpression}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>>
        IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Uri", UriExpression);
    }

    internal void SetModel(StableDiffusionCppModelResource model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (Model is not null)
        {
            throw new InvalidOperationException(
                "stable-diffusion.cpp supports one active model per server resource.");
        }

        Model = model;
    }

    internal void SetHuggingFaceToken(ParameterResource token)
    {
        ArgumentNullException.ThrowIfNull(token);
        HuggingFaceToken = token;
    }
}

#pragma warning restore ASPIREATS001 // AspireExport is experimental
