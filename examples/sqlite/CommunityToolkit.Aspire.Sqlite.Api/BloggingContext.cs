using Microsoft.EntityFrameworkCore;

namespace CommunityToolkit.Aspire.Sqlite.Api;

public class BloggingContext(DbContextOptions<BloggingContext> options) : DbContext(options)
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
}

public class Blog
{
    public int BlogId { get; set; }
    public required string Url { get; set; }

    public List<Post> Posts { get; } = new();
}

public class Post
{
    public int PostId { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }

    public int BlogId { get; set; }
    public Blog? Blog { get; set; }
}