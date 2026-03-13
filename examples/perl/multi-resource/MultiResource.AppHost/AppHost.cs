#pragma warning disable ASPIRECERTIFICATES001

// Demonstrates multi-resource orchestration — multiple Perl resources with
// different package manager configurations, plus a .NET Blazor frontend
// consuming the Perl API via service discovery.

var builder = DistributedApplication.CreateBuilder(args);

var secondLayerApi = builder.AddPerlApi("second-layer-api", ".", "../scripts/secondLayerApi.pl")
    .WithCarton()
    .WithProjectDependencies(cartonDeployment: false)
    .WithLocalLib("local") //to avoid 'sudo' applying to system perl.
    .WithHttpEndpoint(name: "http", env: "PORT")
    .WithHttpsEndpoint(name: "https", env: "HTTPS_PORT")
    .WithHttpsCertificateConfiguration(ctx =>
    {
        ctx.EnvironmentVariables["TLS_CERT"] = ctx.CertificatePath;
        ctx.EnvironmentVariables["TLS_KEY"] = ctx.KeyPath;
        return Task.CompletedTask;
    });

// Perl API using Carton for project-level dependency management.
// appDirectory is "." (the project root) so that:
//   1. carton install runs where cpanfile lives, and
//   2. WithLocalLib() can resolve local/lib/perl5 correctly.
// The script path is relative to that root.
var perlApi = builder.AddPerlApi("perl-api", ".", "../scripts/API.pl")
    .WithCarton()
    .WithProjectDependencies(cartonDeployment: false)
    .WithLocalLib("local") //to avoid 'sudo' applying to system perl.
    .WithEnvironment("SECOND_LAYER_URL", secondLayerApi.GetEndpoint("https"))
    .WithReference(secondLayerApi)
    .WaitFor(secondLayerApi)
    .WithHttpEndpoint(name: "http", env: "PORT")
    .WithHttpsEndpoint(name: "https", env: "HTTPS_PORT")
    .WithHttpsCertificateConfiguration(ctx =>
    {
        // Expose the dev certificate PEM paths to Mojolicious daemon arguments.
        ctx.EnvironmentVariables["TLS_CERT"] = ctx.CertificatePath;
        ctx.EnvironmentVariables["TLS_KEY"] = ctx.KeyPath;
        return Task.CompletedTask;
    });

// Perl worker using cpanm with individual package installs.
// cpanm runs without sudo so modules install to ~/perl5 —
// WithLocalLib pointed there so the worker can find them in @INC.

var perlWorker = builder.AddPerlScript("perl-worker", "../scripts", "Worker.pl")
    .WithCpanMinus()
    .WithPackage("OpenTelemetry::SDK", force: true, skipTest: true)
    .WithLocalLib("local");

// Blazor frontend consuming the Perl API
builder.AddProject<Projects.MultiResource_Driver>("multi-resource-driver")
    .WithExternalHttpEndpoints()
    .WithReference(perlApi);

builder.Build().Run();
