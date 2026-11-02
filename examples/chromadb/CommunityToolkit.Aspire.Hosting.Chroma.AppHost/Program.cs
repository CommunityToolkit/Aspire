using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var chroma = builder.AddChroma("chroma");

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("apiservice", new CommunityToolkit_Aspire_Hosting_Chroma_ApiService().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(chroma)
    .WaitFor(chroma)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
