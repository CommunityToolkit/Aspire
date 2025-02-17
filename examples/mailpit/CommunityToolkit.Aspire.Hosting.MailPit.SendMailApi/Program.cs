using CommunityToolkit.Aspire.Hosting.MailPit.SendMailApi;
using Microsoft.AspNetCore.Mvc;
using System.Data.Common;
using System.Net.Mail;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string? mailPitConnectionString = builder.Configuration.GetConnectionString("mailpit");
DbConnectionStringBuilder connectionBuilder = new()
{
    ConnectionString = mailPitConnectionString 
};

Uri endpoint = new(connectionBuilder["Endpoint"].ToString()!, UriKind.Absolute);
builder.Services.AddScoped(_ => new SmtpClient(endpoint.Host, endpoint.Port));
builder.AddServiceDefaults();
WebApplication app = builder.Build();

app.MapPost("/send", ([FromBody]MailData mailData, [FromServices] SmtpClient smtpClient) =>
    {
        MailMessage myMail = CreateMailMessage(mailData);
        smtpClient.Send(myMail);
    })
.WithName("SendMail");

app.MapGet("/health", () => "OK");

app.MapDefaultEndpoints();
app.Run();
return;

MailMessage CreateMailMessage(MailData mailData)
{
    MailAddress from = new(mailData.From, "TestFromName");
    MailAddress to = new(mailData.To, "TestToName");
    MailMessage mailMessage = new(from, to);
    MailAddress replyTo = new(mailData.From);
    mailMessage.ReplyToList.Add(replyTo);
    mailMessage.Subject = mailData.Subject;
    mailMessage.SubjectEncoding = System.Text.Encoding.UTF8;
    mailMessage.Body = mailData.Body;
    mailMessage.BodyEncoding = System.Text.Encoding.UTF8;
    return mailMessage;
}