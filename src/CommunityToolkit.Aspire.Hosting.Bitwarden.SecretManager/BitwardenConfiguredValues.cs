namespace Aspire.Hosting.ApplicationModel;

// Wraps either a literal GUID or a parameter-backed GUID so resolution and
// manifest/reference generation can go through one code path.
internal sealed class ConfiguredGuidValue
{
    private ConfiguredGuidValue(Guid? literalValue, ParameterResource? parameter)
    {
        LiteralValue = literalValue;
        Parameter = parameter;
    }

    public Guid? LiteralValue { get; }

    public ParameterResource? Parameter { get; }

    public static ConfiguredGuidValue FromLiteral(Guid literalValue) => new(literalValue, null);

    public static ConfiguredGuidValue FromParameter(ParameterResource parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        return new(null, parameter);
    }

    public async Task<Guid> ResolveAsync(
        string resourceName,
        string valueName,
        CancellationToken cancellationToken)
    {
        if (LiteralValue is Guid literalValue)
        {
            return literalValue;
        }

        string? value = await Parameter!
            .GetValueAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!Guid.TryParse(value, out Guid parsedValue))
        {
            throw new DistributedApplicationException(
                $"Bitwarden {valueName} parameter '{Parameter.Name}' for resource '{resourceName}' did not resolve to a valid GUID.");
        }

        return parsedValue;
    }

    public object GetReference(string resourceName, string valueName)
    {
        if (Parameter is not null)
        {
            return Parameter;
        }

        if (LiteralValue is Guid literalValue)
        {
            return literalValue.ToString("D");
        }

        throw new DistributedApplicationException(
            $"Bitwarden resource '{resourceName}' does not have a {valueName} configured.");
    }
}

// Wraps either a literal string or a parameter-backed string while preserving a
// stable pre-resolution identity for state and manifest generation.
internal sealed class ConfiguredStringValue
{
    private ConfiguredStringValue(string? literalValue, ParameterResource? parameter)
    {
        LiteralValue = literalValue;
        Parameter = parameter;
    }

    public string? LiteralValue { get; }

    public ParameterResource? Parameter { get; }

    public static ConfiguredStringValue FromLiteral(string literalValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(literalValue);
        return new(literalValue, null);
    }

    public static ConfiguredStringValue FromParameter(ParameterResource parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        return new(null, parameter);
    }

    public async Task<string> ResolveAsync(
        string resourceName,
        string valueName,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(LiteralValue))
        {
            return LiteralValue;
        }

        string? value = await Parameter!
            .GetValueAsync(cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DistributedApplicationException(
                $"Bitwarden {valueName} parameter '{Parameter.Name}' for resource '{resourceName}' did not resolve to a value.");
        }

        return value;
    }

    public object GetReference(string resourceName, string valueName)
    {
        if (Parameter is not null)
        {
            return Parameter;
        }

        if (!string.IsNullOrWhiteSpace(LiteralValue))
        {
            return LiteralValue;
        }

        throw new DistributedApplicationException(
            $"Bitwarden resource '{resourceName}' does not have a {valueName} configured.");
    }

    public string GetIdentityKey(
        string resourceName,
        string valueName,
        string? resolvedValue = null)
    {
        if (!string.IsNullOrWhiteSpace(resolvedValue))
        {
            return resolvedValue;
        }

        if (!string.IsNullOrWhiteSpace(LiteralValue))
        {
            return LiteralValue;
        }

        // Parameter name is the only stable identity available before the value
        // is resolved.
        if (Parameter is not null)
        {
            return Parameter.Name;
        }

        throw new DistributedApplicationException(
            $"Bitwarden resource '{resourceName}' does not have a {valueName} configured.");
    }

    public string GetDisplayValue(
        string resourceName,
        string valueName,
        string? resolvedValue = null)
        => GetIdentityKey(resourceName, valueName, resolvedValue);
}

internal sealed class BitwardenProjectIdReference(BitwardenSecretManagerResource resource) : IManifestExpressionProvider, IValueProvider, IValueWithReferences
{
    public string ValueExpression => $"{{{resource.Name}.projectId}}";

    IEnumerable<object> IValueWithReferences.References => [resource];

    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(resource.ProjectId?.ToString("D"));
    }
}
