using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CommunityToolkit.Aspire.Supabase.Api.Models;

public class BaseEntity : BaseModel
{
    [PrimaryKey]
    public int Id { get; set; }

    [Column]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}