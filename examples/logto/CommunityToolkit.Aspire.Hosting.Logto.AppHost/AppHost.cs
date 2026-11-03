var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");

var cache = builder.AddRedis("redis");

var logto = builder.AddLogto("logto", postgres)
    .WithRedis(cache)
    .WithDatabaseSeeding();


#pragma warning disable ASPIREDOTNETPROJECT001
var clientOIDC = builder.AddDotnetProject("clientOIDC", new Projects.CommunityToolkit_Aspire_Logto_ClientOIDC().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(logto)
    .WaitFor(logto);
#pragma warning disable ASPIREDOTNETPROJECT001
var clientJWT = builder.AddDotnetProject("clientJWT", new Projects.CommunityToolkit_Aspire_Logto_ClientJWT().ProjectPath)
#pragma warning restore ASPIREDOTNETPROJECT001
    .WithReference(logto)
    .WaitFor(logto);


builder.Build().Run();
