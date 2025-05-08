using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("user", "admin");
var password = builder.AddParameter("password", "adminpasswordreallysecret", secret: true);

var minio = builder.AddMinioContainer("minio", username, password);

builder.AddProject<CommunityToolkit_Aspire_Hosting_Minio_ApiService>("apiservice")
    .WithReference(minio)
    .WaitFor(minio)
    .WithHttpHealthCheck("/health");

builder.Build().Run();