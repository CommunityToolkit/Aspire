using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var mailPit = builder.AddMailPit("mailpit");

var sendmail = builder.AddProject<CommunityToolkit_Aspire_Hosting_MailPit_SendMailApi>("sendmail")
    .WithReference(mailPit)
    .WaitFor(mailPit);

builder.Build().Run();
