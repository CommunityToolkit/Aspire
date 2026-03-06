namespace CommunityToolkit.Aspire.Neon.Api;

internal sealed class NeonApiProjectCreateOptions
{
    public required string ProjectName { get; init; }

    public string? RegionId { get; init; }

    public int? PostgresVersion { get; init; }

    public string? OrganizationId { get; init; }

    public required string BranchName { get; init; }

    public required string DatabaseName { get; init; }

    public required string RoleName { get; init; }
}

internal sealed class NeonApiBranchCreateOptions
{
    public bool? Protected { get; init; }

    public string? InitSource { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public string? ParentLsn { get; init; }

    public DateTimeOffset? ParentTimestamp { get; init; }

    public bool? Archived { get; init; }

    public string EndpointType { get; init; } = "read_write";
}

internal sealed class NeonApiBranchRestoreOptions
{
    public string? SourceBranchId { get; init; }

    public string? SourceLsn { get; init; }

    public DateTimeOffset? SourceTimestamp { get; init; }

    public string? PreserveUnderName { get; init; }
}

internal sealed class NeonApiMaskingRule
{
    public required string DatabaseName { get; init; }

    public required string SchemaName { get; init; }

    public required string TableName { get; init; }

    public required string ColumnName { get; init; }

    public string? MaskingFunction { get; init; }

    public string? MaskingValue { get; init; }
}

internal sealed class NeonApiAnonymizationOptions
{
    public IReadOnlyList<NeonApiMaskingRule> MaskingRules { get; init; } = [];

    public bool StartAnonymization { get; init; } = true;
}