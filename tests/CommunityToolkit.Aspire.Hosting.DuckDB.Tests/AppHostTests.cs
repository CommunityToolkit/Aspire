using CommunityToolkit.Aspire.Testing;
using System.Data.Common;

namespace CommunityToolkit.Aspire.Hosting.DuckDB;

public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_DuckDB_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_DuckDB_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndConnectionStringIsValid()
    {
        var resourceName = "analytics";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var connectionString = await fixture.GetConnectionString(resourceName);

        Assert.NotNull(connectionString);

        var csb = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };
        Assert.True(csb.TryGetValue("DataSource", out var dataSource));
        Assert.NotNull(dataSource);
        Assert.EndsWith(".duckdb", dataSource.ToString()!);
    }

    [Fact]
    public async Task ApiServiceGetsSummary()
    {
        var resourceName = "api";

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName, endpointName: "http");

        var response = await httpClient.GetAsync("/analytics/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await response.Content.ReadAsStringAsync();
        Assert.NotNull(data);
        Assert.NotEmpty(data);
    }

    [Fact]
    public async Task ApiServiceCanCreateOrder()
    {
        var resourceName = "api";

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName, endpointName: "http");

        var createResponse = await httpClient.PostAsync("/analytics/orders", null);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var getResponse = await httpClient.GetAsync("/analytics/orders");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }
}
