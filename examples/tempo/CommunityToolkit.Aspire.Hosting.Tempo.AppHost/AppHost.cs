var builder = DistributedApplication.CreateBuilder(args);

var tempo = builder.AddTempo("tempo");

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("api", new Projects.CommunityToolkit_Aspire_Hosting_Tempo_Api().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(tempo)
    .WithEnvironment("TEMPO_OTLP_ENDPOINT", tempo.Resource.OtlpGrpcEndpoint)
    .WaitFor(tempo);

builder.Build().Run();