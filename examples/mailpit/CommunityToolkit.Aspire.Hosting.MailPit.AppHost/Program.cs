using Microsoft.Extensions.Configuration;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var httpPort = builder.Configuration.GetValue<int?>("MailPit:HttpPort");
var mailPit = builder.AddMailPit("mailpit", httpPort: httpPort);

var sendmail = builder.AddProject<CommunityToolkit_Aspire_Hosting_MailPit_SendMailApi>("sendmail")
    .WithReference(mailPit)
    .WaitFor(mailPit);

builder.Build().Run();
