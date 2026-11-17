# Terraform examples

The C# AppHost uses a local `terraform_data` module; Terraform must be installed.
All operations require an explicit local start. In a separate pipeline workflow,
run init and plan before apply, and use destroy only when intentionally cleaning up.
Terraform state and saved plans may contain secrets.

The TypeScript and Go AppHosts model the same environment. The standalone C#
AppHost demonstrates individual CLI resources without registering pipeline steps:
start init before starting plan. Run each AppHost from its own directory.
