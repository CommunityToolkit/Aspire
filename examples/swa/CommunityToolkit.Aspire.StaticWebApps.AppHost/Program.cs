var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.CommunityToolkit_Aspire_StaticWebApps_ApiApp>("api")
    .WithHttpHealthCheck("/health");

var web = builder
    .AddViteApp("web", Path.Combine(builder.AppHostDirectory, "..", "CommunityToolkit.Aspire.StaticWebApps.WebApp"))
    .WithNpmPackageInstallation()
    //.AddNpmApp("web", Path.Combine(builder.AppHostDirectory, "..", "CommunityToolkit.Aspire.StaticWebApps.WebApp"), "dev")
    .WithHttpHealthCheck("/");

_ = builder
    .AddSwaEmulator("swa")
    .WithAppResource(web)
    .WithApiResource(api);

builder.Build().Run();
