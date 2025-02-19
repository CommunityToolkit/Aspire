using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using System.Data.Common;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.MailPit.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_MailPit_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_MailPit_AppHost>>
{
    private const string ResourceName = "mailpit";
    
    [Fact]
    public async Task ResourceStartsAndMailShouldBeReceived()
    {
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(ResourceName).WaitAsync(TimeSpan.FromMinutes(2));
        SmtpClient smtpClient = await CreateSmtpClient();
        MailMessage myMail = CreateMailMessage();
        HttpClient httpClient = CreateHttpClientForMailPit();

        Exception? exception = Record.Exception(() => smtpClient.Send(myMail));
        
        Assert.Null(exception);
        HttpResponseMessage response = await httpClient.GetAsync("/api/v1/messages");
        response.EnsureSuccessStatusCode();
        
        JsonDocument? responseDocument = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(responseDocument);
        int totalMessageCount = responseDocument.RootElement.GetProperty("messages_count").GetInt32();
        Assert.Equal(1, totalMessageCount);
    }

    private HttpClient CreateHttpClientForMailPit()
    {
        var httpEndpoint = fixture.GetEndpoint(ResourceName, "http");
        return new HttpClient { BaseAddress = httpEndpoint };
    }

    private static MailMessage CreateMailMessage()
    {
        MailAddress from = new("test@test.com", "TestFromName");
        MailAddress to = new("to@test.com", "TestToName");
        MailMessage myMail = new(from, to);
        MailAddress replyTo = new("reply@test.com");
        myMail.ReplyToList.Add(replyTo);
        myMail.Subject = "Subject";
        myMail.SubjectEncoding = System.Text.Encoding.UTF8;
        myMail.Body = "Hello world!";
        myMail.BodyEncoding = System.Text.Encoding.UTF8;
        return myMail;
    }

    private async Task<SmtpClient> CreateSmtpClient()
    {
        string? connectionString = await fixture.GetConnectionString(ResourceName);
        DbConnectionStringBuilder connectionBuilder = new()
        {
            ConnectionString = connectionString 
        };
        Uri connectionUri = new(connectionBuilder["Endpoint"].ToString()!, UriKind.Absolute);
        SmtpClient smtpClient = new(connectionUri.Host, connectionUri.Port);
        return smtpClient;
    }
}