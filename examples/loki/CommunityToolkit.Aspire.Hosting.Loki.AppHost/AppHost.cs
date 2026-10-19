var builder = DistributedApplication.CreateBuilder(args);

var loki = builder.AddLoki("loki")
	.WithHttpHostPort(13100)
	.WithGrpcHostPort(19096);

builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Loki_Api>("api")
	.WithReference(loki)
	.WaitFor(loki);

var _httpEndpoint = loki.Resource.HttpEndpoint;
var _grpcEndpoint = loki.Resource.GrpcEndpoint;

builder.Build().Run();