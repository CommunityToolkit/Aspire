# Terraform hosting integration

Use this integration to model, configure, and orchestrate Terraform modules and CLI commands.

## Getting started

Install Terraform on the machine running the AppHost or pipeline, then add the package:

```shell
aspire add CommunityToolkit.Aspire.Hosting.Terraform
```

## Usage example

```csharp
var infrastructure = builder.AddTerraformEnvironment("infrastructure", "./terraform")
    .WithInit()
    .WithPlan()
    .WithApply(apply => apply.WithExplicitStart())
    .WithDestroy();
```

The local plan waits for init, and apply waits for the saved plan. Destroy requires an explicit local start. Without a declared plan, apply uses `-auto-approve`. Paths are relative to the AppHost directory.

Set input variables using `WithTfVar`, `WithTfVarsFile`, and backend settings using `WithBackendConfig` or `WithBackendConfigFile`. Aspire parameters and expressions can be used for deferred values. `WithWorkspace` selects an existing workspace. `WithTerraformExecutablePath` selects an executable when Terraform is not on `PATH`. Both methods apply to all commands in the environment, whether called before or after declaring them.

For a single command without pipeline steps, use the standalone CLI resource:

```csharp
builder.AddTerraformCli("plan", "./terraform")
    .WithTfVar("message", "Hello from Aspire")
    .WithPlan()
    .WithExplicitStart();
```

Initialize the module before running the standalone plan. See the [examples](../../examples/terraform/) for C#, TypeScript, and Go AppHosts.

For a standalone apply of a saved plan, pass the plan path through `WithApply(args: ["review.tfplan"])`. Variable files configured with `WithTfVarsFile` are omitted for that apply; unrelated `TF_CLI_ARGS_apply` settings remain in effect. Reconfiguring an environment with `WithPlan()` without an output path retains its previously selected path.

## Deployment pipeline

Declared operations register `terraform-init-{name}`, `terraform-plan-{name}`, `terraform-apply-{name}`, and `terraform-destroy-{name}` steps. All wait for Aspire to process parameters, but do not depend on other Terraform steps by default. Apply is required by `aspire deploy` and destroy by `aspire destroy`. Explicit local startup is not an approval gate for pipeline operations.

For approval workflows, run init and plan, securely store the saved plan and module, then restore them and run init on the apply runner before invoking apply. `ChainSteps` instead produces a fresh plan when applying. Saved plans and Terraform state may contain secrets; restrict access to both and to logs. Terraform, provider credentials, backend state, and locking remain your responsibility.

For example, invoke `aspire do terraform-init-infrastructure`, then `aspire do terraform-plan-infrastructure`, and finally `aspire do terraform-apply-infrastructure` after approval. A declared plan is written to the module directory by default; `WithPlan(outputPath: ...)` overrides that location. Applying a saved plan requires the file to exist on the apply runner. Terraform plan exit code 2 (changes present) is treated as success.

Pipeline output redaction is best-effort: known secret parameter values in arguments and environment variables, and values of environment variables with sensitive-looking names, are masked, including individual lines of multiline values. Prefer passing secrets through environment variables: redaction does not hide them from the process command line if passed as arguments. Do not rely on redaction to secure logs; restrict log access and avoid exposing secrets in output. The same precautions apply to local resource logs.

## Additional documentation

See the [Terraform CLI documentation](https://developer.hashicorp.com/terraform/cli).

## Feedback & contributing

See the [contribution guide](https://github.com/CommunityToolkit/Aspire/blob/main/CONTRIBUTING.md).
