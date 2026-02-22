namespace CommunityToolkit.Aspire.Hosting.Neon;

internal sealed class NeonProvisionerDatabaseSpec
{
    public required string ResourceName { get; init; }

    public required string DatabaseName { get; init; }

    public required string RoleName { get; init; }
}

internal sealed class NeonProvisionerDatabaseOutput
{
    public required string ResourceName { get; init; }

    public required string DatabaseName { get; init; }

    public required string RoleName { get; init; }

    public required string ConnectionUri { get; init; }

    public string? Host { get; init; }

    public int? Port { get; init; }

    public string? Password { get; init; }
}

internal sealed class NeonProvisionerOutput
{
    public required string ProjectId { get; init; }

    public required string BranchId { get; init; }

    public required string EndpointId { get; init; }

    public required string DefaultDatabaseName { get; init; }

    public required string DefaultRoleName { get; init; }

    public required string DefaultConnectionUri { get; init; }

    public string? Host { get; init; }

    public int? Port { get; init; }

    public string? Password { get; init; }

    public string? EndpointType { get; init; }

    public string? EndpointRegionId { get; init; }

    public int? EndpointSuspendTimeoutSeconds { get; init; }

    public IReadOnlyList<NeonProvisionerDatabaseOutput> Databases { get; init; } = [];
}
