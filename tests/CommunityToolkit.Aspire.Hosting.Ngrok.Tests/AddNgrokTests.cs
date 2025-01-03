using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Ngrok.Tests;

public class AddNgrokTests
{
    [Fact]
    public void AddNgrokSetsName()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddNgrok("ngrok");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<NgrokResource>());

        Assert.Equal("ngrok", resource.Name);
    }
    
    [Fact]
    public void AddNgrokNullBuilderThrows()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddNgrok("ngrok"));
    }

    [Fact]
    public void AddNgrokNullNameThrows()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddNgrok(null!));
    }

    [Fact]
    public void AddNgrokEmptyNameThrows()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentException>(() => builder.AddNgrok(""));
    }
}