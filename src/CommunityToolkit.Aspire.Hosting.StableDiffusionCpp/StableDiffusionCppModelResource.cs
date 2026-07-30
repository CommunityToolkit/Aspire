using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a model hosted by stable-diffusion.cpp and downloaded from Hugging Face.
/// </summary>
[AspireExport(ExposeProperties = true)]
public sealed class StableDiffusionCppModelResource(
    string name,
    string repository,
    string fileName,
    string revision,
    StableDiffusionCppResource parent)
    : Resource(name),
      IResourceWithParent<StableDiffusionCppResource>,
      IResourceWithConnectionString
{
    /// <summary>
    /// Gets the parent stable-diffusion.cpp server.
    /// </summary>
    public StableDiffusionCppResource Parent { get; } = ThrowIfNull(parent);

    /// <summary>
    /// Gets the Hugging Face repository identifier.
    /// </summary>
    public string Repository { get; } = ThrowIfNull(repository);

    /// <summary>
    /// Gets the model file path inside the Hugging Face repository.
    /// </summary>
    public string FileName { get; } = ThrowIfNull(fileName);

    /// <summary>
    /// Gets the Hugging Face revision.
    /// </summary>
    public string Revision { get; } = ThrowIfNull(revision);

    /// <inheritdoc />
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{Parent.ConnectionStringExpression};Model={Repository}/{FileName};Revision={Revision}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>>
        IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Uri", Parent.UriExpression);
        yield return new("Model", ReferenceExpression.Create($"{Repository}/{FileName}"));
        yield return new("Revision", ReferenceExpression.Create($"{Revision}"));
    }

    private static T ThrowIfNull<T>(
        [NotNull] T? argument,
        [CallerArgumentExpression(nameof(argument))] string? parameterName = null) =>
        argument ?? throw new ArgumentNullException(parameterName);
}

#pragma warning restore ASPIREATS001 // AspireExport is experimental
