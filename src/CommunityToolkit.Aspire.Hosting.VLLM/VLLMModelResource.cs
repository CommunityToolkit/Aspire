using System.Data.Common;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A model served by a vLLM resource.
/// </summary>
/// <remarks>
/// This resource holds connection metadata; the parent server loads the model and owns its lifetime.
/// </remarks>
/// <param name="name">The name of the resource.</param>
/// <param name="model">The model ID or path loaded by the server.</param>
/// <param name="parent">The parent vLLM resource.</param>
[AspireExport(ExposeProperties = true)]
public class VLLMModelResource(string name, string model, VLLMResource parent) :
    Resource(name), IResourceWithParent<VLLMResource>, IResourceWithConnectionString, IResourceWithoutLifetime
{
    /// <summary>
    /// Gets the parent vLLM server.
    /// </summary>
    public VLLMResource Parent { get; } = parent ?? throw new ArgumentNullException(nameof(parent));

    /// <summary>
    /// Gets the model ID or path loaded by the server.
    /// </summary>
    public string ModelName { get; } = ValidateModel(model);

    /// <summary>
    /// Gets the parent endpoint and the served model name.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression
    {
        get
        {
            var model = new DbConnectionStringBuilder { ["Model"] = ServedModelName };
            return ReferenceExpression.Create($"{Parent};{model.ConnectionString}");
        }
    }

    private string ServedModelName => Parent.ServedModelName ?? ModelName;

    /// <inheritdoc />
    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties() =>
        Parent.CombineProperties([new("Model", ReferenceExpression.Create($"{ServedModelName}"))]);

    private static string ValidateModel(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        return model;
    }
}
