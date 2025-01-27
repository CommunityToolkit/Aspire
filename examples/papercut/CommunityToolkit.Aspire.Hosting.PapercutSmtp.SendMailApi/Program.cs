using CommunityToolkit.Aspire.Hosting.PapercutSmtp.SendMailApi;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;

var builder = WebApplication.CreateBuilder(args);

string? papercutConnectionString = builder.Configuration.GetConnectionString("papercut");
Uri papercutUri = new(papercutConnectionString!);
builder.Services.AddScoped(_ => new SmtpClient(papercutUri.Host, papercutUri.Port));
builder.AddServiceDefaults();
WebApplication app = builder.Build();

app.MapPost("/send", ([FromBody]MailData mailData, [FromServices] SmtpClient smtpClient) =>
{
    MailAddress from = new(mailData.From, "TestFromName");
    MailAddress to = new(mailData.To, "TestToName");
    MailMessage myMail = new(from, to);
    MailAddress replyTo = new(mailData.From);
    myMail.ReplyToList.Add(replyTo);
    myMail.Subject = mailData.Subject;
    myMail.SubjectEncoding = System.Text.Encoding.UTF8;
    myMail.Body = mailData.Body;
    myMail.BodyEncoding = System.Text.Encoding.UTF8;
    smtpClient.Send(myMail);
})
.WithName("SendMail");

app.MapDefaultEndpoints();
app.Run();