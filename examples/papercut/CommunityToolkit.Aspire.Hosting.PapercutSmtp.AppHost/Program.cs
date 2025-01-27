using Aspire.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var papercut = builder.AddPapercutSmtp("papercut");

var sendmail = builder.AddProject<CommunityToolkit_Aspire_Hosting_PapercutSmtp_SendMailApi>("sendmail")
    .WithReference(papercut)
    .WaitFor(papercut);

builder.Build().Run();
