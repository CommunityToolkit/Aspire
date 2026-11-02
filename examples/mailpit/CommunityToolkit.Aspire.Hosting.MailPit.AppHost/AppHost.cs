using Microsoft.Extensions.Configuration;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var httpPort = builder.Configuration.GetValue<int?>("MailPit:HttpPort");
var mailPit = builder.AddMailPit("mailpit", httpPort: httpPort);

#pragma warning disable ASPIREDOTNETPROJECT001
var sendmail = builder.AddDotnetProject("sendmail", new CommunityToolkit_Aspire_Hosting_MailPit_SendMailApi().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(mailPit)
    .WaitFor(mailPit);

builder.Build().Run();
