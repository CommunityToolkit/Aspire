var builder = DistributedApplication.CreateBuilder(args);

var init = builder.AddTerraformCli("init", "../module")
    .WithInit()
    .WithExplicitStart();

builder.AddTerraformCli("plan", "../module")
    .WithTfVar("message", "Hello from Aspire")
    .WithPlan()
    .WaitForCompletion(init)
    .WithExplicitStart();

builder.Build().Run();
