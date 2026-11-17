using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var environment = builder.AddTerraformEnvironment("sample", builder.Configuration["TerraformDirectory"] ?? "../module")
    .WithTerraformExecutablePath(builder.Configuration["TerraformExecutable"] ?? "terraform")
    .WithTfVar("message", "Hello from Aspire")
    .WithInit(init => init.WithExplicitStart())
    .WithPlan(configure: plan => plan.WithExplicitStart())
    .WithApply(apply => apply.WithExplicitStart().WithArgs("-lock-timeout=60s"))
    .WithDestroy();

if (builder.Configuration["TerraformVarsFile"] is { } varsFile)
{
    environment.WithEnvironment("TF_CLI_ARGS_apply", "-lock-timeout=60s")
        .WithTfVarsFile(varsFile);
}

if (builder.Configuration["TerraformTestSecretEnabled"] == "true")
{
    var secret = builder.AddParameter("pipeline-value", secret: true);
    environment.WithTfVar("probe", ReferenceExpression.Create($"prefix-{secret}-suffix"));
}

builder.Build().Run();