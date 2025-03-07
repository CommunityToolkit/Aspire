# CommunityToolkit.Aspire.Hosting.Dapr.AzureExtensions library

This package provides extension methods and resource definitions that support the development of  packages that integrate **Dapr** with **Azure** resources as part of an Aspire Hosting application.

## Functionality

1. **`AzureDaprComponentResource`**  
   A resource that defines 'extends' AzureProvisioningResource. This resource currently contains no additional functionality but ensures API consistency as well as resource identification when extending infrastructure configuration

2. **`AddAzureDaprResource`**  
   An extension method that configures an `AzureDaprComponentResource` and integrates it into the Aspire Hosting resource builder pipeline.

3. **`GetInfrastructureConfigurationAction`**  
   Provides a reusable action that sets up a Container App Managed Environment for hosting Dapr components. It also handles naming and parameter configuration using Bicep functions.

4. **`ConfigureKeyVaultSecrets`**  
   An extension method that configures Key Vault secrets and attaches them to an existing infrastructure setup, allowing your Dapr component (and other Azure services) to securely access secrets.

5. **`CreateDaprComponent`**  
   A factory-like method to quickly instantiate a Dapr component with the specified type and version.

## Notes

  This designed to be consumed by more focused packages (such as `CommunityToolkit.Aspire.Hosting.Redis.Dapr.Azure`), which build on top of these shared infrastructure definitions.