﻿using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an ollama model.
/// </summary>
/// <param name="name">The name for the resource.</param>
/// <param name="modelName">The name of the LLM model, can include a tag.</param>
/// <param name="parent">The <see cref="OllamaResource"/> parent.</param>
public class OllamaModelResource(string name, string modelName, OllamaResource parent) : Resource(name), IResourceWithParent<OllamaResource>, IResourceWithConnectionString
{
    /// <summary>
    /// Gets the parent Ollama container resource.
    /// </summary>
    public OllamaResource Parent { get; } = ThrowIfNull(parent);

    /// <summary>
    /// Gets the connection string expression for the Ollama model.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"{Parent};Model={ModelName}");

    /// <summary>
    /// Gets the model name.
    /// </summary>
    public string ModelName { get; } = ThrowIfNull(modelName);

    private static T ThrowIfNull<T>([NotNull] T? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        => argument ?? throw new ArgumentNullException(paramName);
}
