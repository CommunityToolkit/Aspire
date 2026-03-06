namespace CommunityToolkit.Aspire.Hosting.Neon;

internal static class NeonProvisionerEnvironmentVariables
{
    public const string ApiKey = "NEON_API_KEY";
    public const string Mode = "NEON_MODE";
    public const string OutputFilePath = "NEON_OUTPUT_FILE_PATH";

    public const string ProjectId = "NEON_PROJECT_ID";
    public const string ProjectName = "NEON_PROJECT_NAME";
    public const string CreateProjectIfMissing = "NEON_CREATE_PROJECT_IF_MISSING";
    public const string RegionId = "NEON_REGION_ID";
    public const string PostgresVersion = "NEON_POSTGRES_VERSION";
    public const string OrganizationId = "NEON_ORGANIZATION_ID";
    public const string OrganizationName = "NEON_ORGANIZATION_NAME";

    public const string BranchId = "NEON_BRANCH_ID";
    public const string BranchName = "NEON_BRANCH_NAME";
    public const string ParentBranchId = "NEON_PARENT_BRANCH_ID";
    public const string ParentBranchName = "NEON_PARENT_BRANCH_NAME";
    public const string BranchProtected = "NEON_BRANCH_PROTECTED";
    public const string BranchInitSource = "NEON_BRANCH_INIT_SOURCE";
    public const string BranchExpiresAt = "NEON_BRANCH_EXPIRES_AT";
    public const string BranchParentLsn = "NEON_BRANCH_PARENT_LSN";
    public const string BranchParentTimestamp = "NEON_BRANCH_PARENT_TIMESTAMP";
    public const string BranchArchived = "NEON_BRANCH_ARCHIVED";
    public const string CreateBranchIfMissing = "NEON_CREATE_BRANCH_IF_MISSING";
    public const string BranchSetAsDefault = "NEON_BRANCH_SET_AS_DEFAULT";
    public const string UseEphemeralBranch = "NEON_USE_EPHEMERAL_BRANCH";
    public const string EphemeralBranchPrefix = "NEON_EPHEMERAL_BRANCH_PREFIX";

    public const string BranchRestoreEnabled = "NEON_BRANCH_RESTORE_ENABLED";
    public const string BranchRestoreSourceBranchId = "NEON_BRANCH_RESTORE_SOURCE_BRANCH_ID";
    public const string BranchRestoreSourceLsn = "NEON_BRANCH_RESTORE_SOURCE_LSN";
    public const string BranchRestoreSourceTimestamp = "NEON_BRANCH_RESTORE_SOURCE_TIMESTAMP";
    public const string BranchRestorePreserveUnderName = "NEON_BRANCH_RESTORE_PRESERVE_UNDER_NAME";

    public const string BranchAnonymizationEnabled = "NEON_BRANCH_ANONYMIZATION_ENABLED";
    public const string BranchAnonymizationStart = "NEON_BRANCH_ANONYMIZATION_START";
    public const string BranchMaskingRulesJson = "NEON_BRANCH_MASKING_RULES_JSON";

    public const string EndpointId = "NEON_ENDPOINT_ID";
    public const string EndpointType = "NEON_ENDPOINT_TYPE";
    public const string CreateEndpointIfMissing = "NEON_CREATE_ENDPOINT_IF_MISSING";

    public const string DatabaseName = "NEON_DATABASE_NAME";
    public const string RoleName = "NEON_ROLE_NAME";
    public const string UseConnectionPooler = "NEON_USE_CONNECTION_POOLER";
    public const string DatabaseSpecsJson = "NEON_DATABASE_SPECS_JSON";
}
