using CommunityToolkit.Aspire.Posta.Clients;
using CommunityToolkit.Aspire.Posta.Configuration;
using CommunityToolkit.Aspire.Posta.Endpoints;
using CommunityToolkit.Aspire.Posta.Models.Emails;
using CommunityToolkit.Aspire.Posta.Transport;
using System.Net.Http.Headers;
using System.Text;

namespace CommunityToolkit.Aspire.Posta.Tests;

public class PostaClientTests
{
    private static readonly string[] s_queryTags = ["one", "two"];

    [Fact]
    public void EndpointCatalogContainsEveryOpenApiOperation()
    {
        var endpoints = typeof(IPostaEndpoints).GetProperties();

        Assert.Equal(21, endpoints.Length);
        Assert.All(endpoints, property => Assert.Equal(typeof(PostaEndpoint), property.PropertyType));
    }

    [Fact]
    public void SectionInterfacesContainEveryNamedOperation()
    {
        Type[] sections =
        [
            typeof(IPostaEmailsClient), typeof(IPostaTemplatesClient), typeof(IPostaSubscriberListsClient),
            typeof(IPostaHealthClient), typeof(IPostaInfoClient)
        ];

        int operationCount = sections.Sum(section => section.GetMethods().Count(method => method.DeclaringType == section));

        Assert.Equal(21, operationCount);
    }

    [Fact]
    public async Task NamedOperationSerializesRequestAndDeserializesTypedResponse()
    {
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true,\"data\":{\"id\":\"email-id\",\"status\":\"queued\"}}", Encoding.UTF8, "application/json")
        });
        PostaTransport transport = CreateTransport(handler, apiKey: "secret-key");
        PostaClientSection client = new(transport, new PostaEndpoints());

        SendAnEmailResponse? response = await client.SendAnEmailAsync(
            new SendAnEmailRequest
            {
                From = "sender@example.com",
                To = ["recipient@example.com"],
                Subject = "Hello",
                DryRun = true
            },
            TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal("email-id", response.Data?.Id);
        Assert.Equal("queued", response.Data?.Status);
        Assert.Equal("/api/v1/emails/send?dry_run=true", handler.Request!.RequestUri!.PathAndQuery);
        Assert.Contains("\"from\":\"sender@example.com\"", handler.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("dry_run", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsyncExpandsPathAndQueryAndUsesApiKey()
    {
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"queued\"}", Encoding.UTF8, "application/json")
        });
        PostaTransport transport = CreateTransport(handler, apiKey: "secret-key");

        using var result = await transport.SendAsync(
            new PostaEndpoint(HttpMethod.Post, "/api/v1/emails/{id}", PostaAuthentication.ApiKey),
            new PostaRequest
            {
                PathParameters = new Dictionary<string, object?> { ["id"] = "a/b" },
                Query = new Dictionary<string, object?> { ["dry_run"] = true, ["tag"] = s_queryTags },
                Body = new { subject = "Hello" }
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("/api/v1/emails/a%2Fb?dry_run=true&tag=one&tag=two", handler.Request!.RequestUri!.PathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "secret-key"), handler.Request.Headers.Authorization);
        Assert.Contains("\"subject\":\"Hello\"", handler.Body, StringComparison.Ordinal);
        Assert.Equal("queued", result!.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task SendAsyncDoesNotSendCredentialToAnonymousEndpoint()
    {
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });
        PostaTransport transport = CreateTransport(handler, apiKey: "must-not-leak");

        using var result = await transport.SendAsync(
            new PostaEndpoint(HttpMethod.Get, "/healthz", PostaAuthentication.None),
            null,
            TestContext.Current.CancellationToken);

        Assert.Null(handler.Request!.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsyncPreservesErrorResponse()
    {
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{\"error\":\"quota exceeded\"}", Encoding.UTF8, "application/json")
        });
        PostaTransport transport = CreateTransport(handler, apiKey: "key");

        PostaApiException exception = await Assert.ThrowsAsync<PostaApiException>(() => transport.SendAsync(
            new PostaEndpoint(HttpMethod.Post, "/api/v1/emails/send", PostaAuthentication.ApiKey),
            null,
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Contains("quota exceeded", exception.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public void EndpointCanBeOverridden()
    {
        IPostaEndpoints endpoints = new CustomEndpoints();

        Assert.Equal("/custom/emails/send", endpoints.SendAnEmail.Path);
    }

    private static PostaTransport CreateTransport(RecordingHandler handler, string? apiKey = null, string? accessToken = null)
    {
        HttpClient client = new(handler) { BaseAddress = new Uri("https://posta.example") };
        return new PostaTransport(client, new PostaCredentialProvider(new PostaClientSettings
        {
            ApiKey = apiKey,
            AccessToken = accessToken
        }));
    }

    private sealed class CustomEndpoints : PostaEndpoints
    {
        public override PostaEndpoint SendAnEmail { get; } = new(HttpMethod.Post, "/custom/emails/send", PostaAuthentication.ApiKey);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}