namespace CommunityToolkit.Aspire.Hosting.Supabase.Sync;

/// <summary>
/// Specifies what to synchronize from an online Supabase project.
/// </summary>
[Flags]
public enum SyncOptions
{
    /// <summary>
    /// No synchronization.
    /// </summary>
    None = 0,

    /// <summary>
    /// Sync table structures (columns, types, constraints).
    /// </summary>
    Schema = 1 << 0,

    /// <summary>
    /// Sync table data.
    /// </summary>
    Data = 1 << 1,

    /// <summary>
    /// Sync Row Level Security policies.
    /// </summary>
    Policies = 1 << 2,

    /// <summary>
    /// Sync stored procedures and functions.
    /// </summary>
    Functions = 1 << 3,

    /// <summary>
    /// Sync database triggers.
    /// </summary>
    Triggers = 1 << 4,

    /// <summary>
    /// Sync storage buckets.
    /// </summary>
    StorageBuckets = 1 << 5,

    /// <summary>
    /// Sync storage files (downloads files from remote storage).
    /// </summary>
    StorageFiles = 1 << 6,

    /// <summary>
    /// Sync custom types and enums.
    /// </summary>
    Types = 1 << 7,

    /// <summary>
    /// Sync views.
    /// </summary>
    Views = 1 << 8,

    /// <summary>
    /// Sync indexes.
    /// </summary>
    Indexes = 1 << 9,

    /// <summary>
    /// Sync Edge Functions from the remote project.
    /// Requires Supabase Management API token (personal access token from Dashboard → Account → Access Tokens).
    /// </summary>
    EdgeFunctions = 1 << 10,

    /// <summary>
    /// All schema-related options (Schema, Policies, Functions, Triggers, Types, Views, Indexes).
    /// Requires database password.
    /// </summary>
    AllSchema = Schema | Policies | Functions | Triggers | Types | Views | Indexes,

    /// <summary>
    /// All storage-related options (StorageBuckets, StorageFiles).
    /// </summary>
    AllStorage = StorageBuckets | StorageFiles,

    /// <summary>
    /// Everything - complete sync of all database objects, data, storage, and Edge Functions.
    /// Requires database password and Management API token for Edge Functions.
    /// </summary>
    All = AllSchema | Data | AllStorage | EdgeFunctions
}
