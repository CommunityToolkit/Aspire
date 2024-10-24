namespace CommunityToolkit.Aspire.Marten.ApiService;

public class Movie
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public List<string>? Genres { get; set; }
}