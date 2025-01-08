using CommunityToolkit.Aspire.Testing;
using FluentAssertions;
using System.Data.Common;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.Sqlite;

public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Sqlite_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Sqlite_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndDbFileExists()
    {
        var resourceName = "sqlite";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var connectionString = await fixture.GetConnectionString(resourceName);

        Assert.NotNull(connectionString);

        var csb = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };
        Assert.True(csb.TryGetValue("Data Source", out var dataSource));

        Assert.True(File.Exists(dataSource.ToString()));
    }

    [Fact]
    public async Task ApiServiceCreateTestItemWithSqliteClient()
    {
        var resourceName = "api";

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var createResponse = await httpClient.PostAsJsonAsync("/test", "test");
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await httpClient.GetAsync("/test");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await getResponse.Content.ReadAsStringAsync();
        Assert.NotNull(data);
        Assert.NotEmpty(data);
    }

    [Fact]
    public async Task ApiServiceCreateBlogItem()
    {
        var resourceName = "api";

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var createResponse = await httpClient.PostAsJsonAsync("/blog", new { Url = "https://example.com" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await httpClient.GetAsync("/blog");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await getResponse.Content.ReadFromJsonAsync<List<Blog>>();
        Assert.NotNull(data);
        Assert.Single(data);
        Assert.Equal("https://example.com", data.First().Url);
    }

    public class Blog
    {
        public int BlogId { get; set; }
        public required string Url { get; set; }

    }
}