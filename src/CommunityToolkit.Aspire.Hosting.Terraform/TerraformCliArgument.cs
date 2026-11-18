// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resolves a single shell-quoted Terraform argument for a <c>TF_CLI_ARGS_*</c> environment variable.
/// </summary>
/// <remarks>
/// Terraform splits these variables with shell-like rules, so the resolved value is single-quoted to preserve
/// spaces, backslashes, and quotes in paths or secrets.
/// </remarks>
/// <param name="prefix">The argument prefix preceding the value.</param>
/// <param name="value">The literal or deferred argument value.</param>
internal sealed class TerraformCliArgument(string prefix, object value)
    : IValueProvider, IManifestExpressionProvider, IValueWithReferences
{
    /// <inheritdoc />
    public string ValueExpression => value switch
    {
        string literal => Quote(prefix + literal),
        IManifestExpressionProvider provider => Quote(prefix + provider.ValueExpression),
        _ => throw new InvalidOperationException("Unsupported Terraform argument value.")
    };

    /// <inheritdoc />
    public IEnumerable<object> References => value is string ? [] : [value];

    /// <inheritdoc />
    public async ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default)
    {
        var resolved = value switch
        {
            string literal => literal,
            IValueProvider provider => await provider.GetValueAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Unsupported Terraform argument value.")
        };

        return Quote(prefix + (resolved ?? throw new InvalidOperationException("The Terraform argument value could not be resolved.")));
    }

    // Terraform's parser does not unescape \' outside quotes, so embedded single quotes are double-quoted instead.
    /// <summary>Quotes an argument using the rules accepted by Terraform's environment argument parser.</summary>
    /// <param name="argument">The argument to quote.</param>
    /// <returns>The quoted argument.</returns>
    internal static string Quote(string argument) => $"'{argument.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
}