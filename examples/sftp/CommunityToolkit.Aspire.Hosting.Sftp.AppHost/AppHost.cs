using Aspire.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var sftp = builder.AddSftp("sftp").WithEnvironment("SFTP_USERS", "foo:$5$t9qxNlrcFqVBNnad$U27ZrjbKNjv4JkRWvi6MjX4x6KXNQGr8NTIySOcDgi4:e:::uploads");

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("api", new Projects.CommunityToolkit_Aspire_Hosting_Sftp_ApiService().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(sftp)
    .WaitForStart(sftp);

builder.Build().Run();
