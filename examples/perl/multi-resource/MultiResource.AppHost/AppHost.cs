// Demonstrates multi-resource orchestration — multiple Perl resources with
// different package manager configurations, plus a .NET Blazor frontend
// consuming the Perl API via service discovery.

var builder = DistributedApplication.CreateBuilder(args);

// Perl API using Carton for project-level dependency management.
// appDirectory is "." (the project root) so that:
//   1. carton install runs where cpanfile lives, and
//   2. WithLocalLib() can resolve local/lib/perl5 correctly.
// The script path is relative to that root.
var perlApi = builder.AddPerlApi("perl-api", ".", "../scripts/API.pl")
    .WithCarton()
    .WithProjectDependencies(deployment: false)
    .WithLocalLib()
    .WithHttpEndpoint(targetPort: 3031, name: "http", env: "PORT");

// Perl worker using cpanm with individual package installs.
// cpanm runs without sudo so modules install to ~/perl5 —
// WithLocalLib pointed there so the worker can find them in @INC.
var perl5Home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "perl5");
var perlWorker = builder.AddPerlScript("perl-worker", "../scripts", "Worker.pl")
    .WithCpanMinus()
    .WithPackage("OpenTelemetry::SDK", force: true, skipTest: true)
    .WithLocalLib(perl5Home);

// Blazor frontend consuming the Perl API
builder.AddProject<Projects.MultiResource_Driver>("multi-resource-driver")
    .WithExternalHttpEndpoints()
    .WithReference(perlApi);

builder.Build().Run();
