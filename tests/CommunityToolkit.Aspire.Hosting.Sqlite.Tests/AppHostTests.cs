using CommunityToolkit.Aspire.Testing;
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
    public async Task SqliteWebResourceStarts()
    {
        var resourceName = "sqlite-sqliteweb";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName, endpointName: "http");

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("SQLite Web", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ApiServiceCreateTestItemWithSqliteClient()
    {
        var resourceName = "api";

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName, endpointName: "http");

        var createResponse = await httpClient.PostAsJsonAsync("/test", "test");
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var getResponse = await httpClient.GetAsync("/test");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var data = await getResponse.Content.ReadAsStringAsync();
        Assert.NotNull(data);
        Assert.NotEmpty(data);
    }

    [Fact]
    public async Task ApiServiceCreateBlogItem()
    {
        var resourceName = "api";

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName, endpointName: "http");

        var expectedUrl = $"https://example.com/{Guid.NewGuid()}";
        var createResponse = await httpClient.PostAsJsonAsync("/blog", new { Url = expectedUrl });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdBlog = await createResponse.Content.ReadFromJsonAsync<Blog>();
        Assert.NotNull(createdBlog);

        var getResponse = await httpClient.GetAsync($"/blog/{createdBlog.BlogId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var data = await getResponse.Content.ReadFromJsonAsync<Blog>();
        Assert.NotNull(data);
        Assert.Equal(expectedUrl, data.Url);
    }

    public class Blog
    {
        public int BlogId { get; set; }
        public required string Url { get; set; }

    }
}
