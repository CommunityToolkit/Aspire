namespace CommunityToolkit.Aspire.Hosting.Supabase;

/// <summary>
/// Options to enable or disable Supabase modules.
/// </summary>
public class SupabaseModuleOptions
{
    public bool EnableVector { get; set; } = true;
    public bool EnableAuth { get; set; } = true;
    public bool EnableMinio { get; set; } = true;
    public bool EnableStorage { get; set; } = true;
    public bool EnableEdgeFunctions { get; set; } = true;
}