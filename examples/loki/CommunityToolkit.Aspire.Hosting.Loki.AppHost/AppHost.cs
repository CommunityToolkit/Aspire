var builder = DistributedApplication.CreateBuilder(args);

var loki = builder.AddLoki("loki", settings =>
{
	settings.HttpPort = 13100;
	settings.GrpcPort = 19096;
});

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Loki_Api>("api")
	.WithReference(loki)
	.WaitFor(loki);

var _httpEndpoint = loki.Resource.PrimaryEndpoint;
var _grpcEndpoint = loki.Resource.GrpcEndpoint;

builder.Build().Run();