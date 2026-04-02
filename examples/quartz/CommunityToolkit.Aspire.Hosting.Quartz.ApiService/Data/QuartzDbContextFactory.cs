using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuartzSample.ApiService.Data;

/// <summary>
/// Design-time factory for EF Core migrations
/// </summary>
public class QuartzDbContextFactory : IDesignTimeDbContextFactory<QuartzDbContext>
{
    public QuartzDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<QuartzDbContext>();

        // Use a dummy connection string for design-time
        optionsBuilder.UseNpgsql("Host=localhost;Database=quartzdb;Username=postgres;Password=postgres");

        return new QuartzDbContext(optionsBuilder.Options, configuration);
    }
}
