namespace CommunityToolkit.Aspire.Hosting.Supabase;

internal static class SupabaseContainerImageTags
{
    public const string Registry = "docker.io";

    public const string PostgresImage = "supabase/postgres";
    public const string PostgresTag = "17.4.1.042";

    public const string KongImage = "kong";
    public const string KongTag = "2.8.1";

    public const string StudioImage = "supabase/studio";
    public const string StudioTag = "2025.06.02-sha-8f2993d";

    public const string RestImage = "postgrest/postgrest";
    public const string RestTag = "v12.2.12";

    public const string RealtimeImage = "supabase/realtime";
    public const string RealtimeTag = "v2.34.47";

    public const string StorageImage = "supabase/storage-api";
    public const string StorageTag = "v1.23.0";

    public const string AuthImage = "supabase/gotrue";
    public const string AuthTag = "v2.174.0";

    public const string MetaImage = "supabase/postgres-meta";
    public const string MetaTag = "v0.89.3";

    public const string InbucketImage = "inbucket/inbucket";
    public const string InbucketTag = "v3.0.3";

    public const string ImageProxyImage = "darthsim/imgproxy";
    public const string ImageProxyTag = "v3.8.0";

    public const string LogflareImage = "supabase/logflare";
    public const string LogflareTag = "1.14.2";

    public const string EdgeRuntimeImage = "supabase/edge-runtime";
    public const string EdgeRuntimeTag = "v1.67.4";
}