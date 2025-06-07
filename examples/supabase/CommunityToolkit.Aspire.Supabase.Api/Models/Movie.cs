using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CommunityToolkit.Aspire.Supabase.Api.Models;

[Table("movie")]
public class Movie : BaseEntity
{
    [Column]
    public string Title { get; set; } = string.Empty;
    
    [Column]
    public string Director { get; set; } = string.Empty;
    
    [Column]
    public int YearReleased { get; set; }
    
    [Column]
    public string Genre { get; set; } = string.Empty;
}