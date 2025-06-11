using Microsoft.Extensions.DependencyInjection;
using Supabase.Postgrest.Responses;

namespace CommunityToolkit.Aspire.Supabase.Client;

public static class SupabaseExtensions
{
    //TODO
    public static void AddSupabaseClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        services.AddScoped<Supabase.Client>(
            provider => new Supabase.Client(
                url,
                key,
                new Supabase.SupabaseOptions
                {
                    AutoConnectRealtime = true,
                }
            )
        );
    }
}