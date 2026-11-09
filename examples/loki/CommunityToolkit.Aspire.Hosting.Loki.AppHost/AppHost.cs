var builder = DistributedApplication.CreateBuilder(args);

var loki = builder.AddLoki("loki")
	.WithHttpHostPort(13100)
	.WithGrpcHostPort(19096);

#pragma warning disable ASPIREDOTNETPROJECT001
builder.AddDotnetProject("api", new Projects.CommunityToolkit_Aspire_Hosting_Loki_Api().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
	.WithReference(loki)
	.WaitFor(loki);

var _httpEndpoint = loki.Resource.HttpEndpoint;
var _grpcEndpoint = loki.Resource.GrpcEndpoint;

builder.Build().Run();