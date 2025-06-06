using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Apache.Tika.Tests;

public class AddApacheTikaTests
{
    [Fact]
    public void AddApacheTikaResource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddApacheTika("tika");
        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var apacheTikaResource = Assert.Single(appModel.Resources.OfType<ApacheTikaResource>());
        Assert.Equal("tika", apacheTikaResource.Name);
    }
}
