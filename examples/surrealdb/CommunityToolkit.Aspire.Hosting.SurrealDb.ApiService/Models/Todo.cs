using SurrealDb.Net.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityToolkit.Aspire.Hosting.SurrealDb.ApiService.Models;

[Table(Table)]
public class Todo : Record
{
    internal const string Table = "todo";

    [Column("title")]
    public string? Title { get; set; }
    
    [Column("due_by")]
    public DateOnly? DueBy { get; set; } = null;
    
    [Column("is_complete")]
    public bool IsComplete { get; set; } = false;
}