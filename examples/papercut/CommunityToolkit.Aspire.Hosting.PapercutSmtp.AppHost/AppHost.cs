using Aspire.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var papercut = builder.AddPapercutSmtp("papercut");

#pragma warning disable ASPIREDOTNETPROJECT001
var sendmail = builder.AddDotnetProject("sendmail", new CommunityToolkit_Aspire_Hosting_PapercutSmtp_SendMailApi().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(papercut)
    .WaitFor(papercut);

builder.Build().Run();
