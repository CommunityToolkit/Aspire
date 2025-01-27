using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using System.Net.Mail;

namespace CommunityToolkit.Aspire.Hosting.PapercutSmtp.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_PapercutSmtp_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_PapercutSmtp_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndSendingMailNoException()
    {
        const string resourceName = "papercut";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(2));
        string? connectionString = await fixture.GetConnectionString("papercut");
        Uri connectionUri = new(connectionString!);
        SmtpClient smtpClient = new SmtpClient(connectionUri.Host, connectionUri.Port);
        MailAddress from = new("test@test.com", "TestFromName");
        MailAddress to = new("to@test.com", "TestToName");
        MailMessage myMail = new(from, to);
        MailAddress replyTo = new("reply@test.com");
        myMail.ReplyToList.Add(replyTo);
        myMail.Subject = "Subject";
        myMail.SubjectEncoding = System.Text.Encoding.UTF8;
        myMail.Body = "Hello world!";
        myMail.BodyEncoding = System.Text.Encoding.UTF8;
        Exception? exception = Record.Exception(() => smtpClient.Send(myMail));
        Assert.Null(exception);
    }
}