namespace CommunityToolkit.Aspire.Hosting.Supabase;

/// <summary>
/// Options to enable or disable Supabase modules.
/// </summary>
public class SupabaseModuleOptions
{
    /// <summary>
    /// Enable or disable the Supabase Realtime container.
    /// </summary>
    public bool EnableVector { get; set; } = true;
    /// <summary>
    /// Enable or disable the Supabase Auth container.
    /// </summary>
    public bool EnableAuth { get; set; } = true;
    
    /// <summary>
    /// Enable or disable the Supabase Postgres container.
    /// </summary>
    public bool EnableMinio { get; set; } = true;
    
    /// <summary>
    /// Enable or disable the Supabase Storage container.
    /// </summary>
    public bool EnableStorage { get; set; } = true;
    
    /// <summary>
    /// Enable or disable the Supabase Edge Functions container.
    /// </summary>
    public bool EnableEdgeFunctions { get; set; } = true;
}