#pragma warning disable ASPIRECERTIFICATES001

var builder = DistributedApplication.CreateBuilder(args);

builder.AddPerlApi("carton-api", ".", "../scripts/API.pl")
    .WithCarton()
    .WithProjectDependencies(deployment: false)
    .WithLocalLib()
    .WithHttpEndpoint(name: "http", env: "PORT")
    .WithHttpsEndpoint(name: "https", env: "HTTPS_PORT")
    .WithHttpsCertificateConfiguration(ctx =>
    {
        ctx.EnvironmentVariables["TLS_CERT"] = ctx.CertificatePath;
        ctx.EnvironmentVariables["TLS_KEY"] = ctx.KeyPath;
        return Task.CompletedTask;
    });

builder.Build().Run();
