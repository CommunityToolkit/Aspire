namespace CommunityToolkit.Aspire.Hosting.Supabase;

internal static class SupabaseContainerImageTags
{
    public const string Registry = "docker.io";

    public const string PostgresImage = "supabase/postgres";
    public const string PostgresTag = "15.1.0.63";

    public const string KongImage = "supabase/kong";
    public const string KongTag = "0.1.0";

    public const string StudioImage = "supabase/studio";
    public const string StudioTag = "20240111-4b8d3e6";

    public const string RestImage = "supabase/postgrest";
    public const string RestTag = "v11.2.0";

    public const string RealtimeImage = "supabase/realtime";
    public const string RealtimeTag = "v2.34.8";

    public const string StorageImage = "supabase/storage-api";
    public const string StorageTag = "v0.48.6";

    public const string AuthImage = "supabase/gotrue";
    public const string AuthTag = "v2.150.0";

    public const string MetaImage = "supabase/postgres-meta";
    public const string MetaTag = "v0.79.0";

    public const string InbucketImage = "inbucket/inbucket";
    public const string InbucketTag = "v3.0.3";

    public const string ImageProxyImage = "supabase/image-proxy";
    public const string ImageProxyTag = "v2.6.0";

    public const string LogflareImage = "supabase/logflare";
    public const string LogflareTag = "v0.8.1";

    public const string EdgeRuntimeImage = "supabase/edge-runtime";
    public const string EdgeRuntimeTag = "v1.41.2";
}