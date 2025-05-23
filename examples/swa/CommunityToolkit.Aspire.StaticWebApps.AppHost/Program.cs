var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.CommunityToolkit_Aspire_StaticWebApps_ApiApp>("api")
    .WithHttpHealthCheck("/health");

var web = builder
    .AddViteApp("web", Path.Combine(builder.AppHostDirectory, "..", "CommunityToolkit.Aspire.StaticWebApps.WebApp"))
    .WithNpmPackageInstallation()
    .WithHttpHealthCheck("/");

#pragma warning disable CTASPIRE003 // Type or member is obsolete
_ = builder
    .AddSwaEmulator("swa")
    .WithAppResource(web)
    .WithApiResource(api);
#pragma warning restore CTASPIRE003 // Type or member is obsolete

builder.Build().Run();
