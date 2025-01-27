using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Hosting.PapercutSmtp.SendMailApi;
using CommunityToolkit.Aspire.Testing;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.PapercutSmtp.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_PapercutSmtp_SendMailApi> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_PapercutSmtp_SendMailApi>>
{
    [Fact]
    public async Task ResourceStartsAndSendingMailRespondsOk()
    {
        const string resourceName = "sendmail";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(2));
        HttpClient httpClient = fixture.CreateHttpClient(resourceName);

        MailData payload = new()
        {
            Body = "Hello World",
            From = "from@test.com",
            To = "to@test.com",
            Subject = "subject"
        };
        HttpResponseMessage response = await httpClient.PostAsync("/send", JsonContent.Create(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}