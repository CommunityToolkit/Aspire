# CommunityToolkit.Aspire.Hosting.Dapr.AzureRedis

This package extends [CommunityToolkit.Aspire.Hosting.Dapr.Azure] by adding specialized integration for **Azure Redis** as a Dapr state store in Aspire Hosting applications.

It provides a convenient way to configure an **Azure Redis** resource so that Dapr components (specifically, a Redis state store) can integrate smoothly into the Aspire deployment pipeline.


---

## Features

1. **`WithReference(IResourceBuilder<AzureRedisCacheResource>)`**  
   Attaches the Azure Redis configuration to an existing Dapr component resource. 
   - Automatically sets up parameters for **Redis Host** and handles **TLS** configuration.  
   - Integrates secret management if Azure Redis requires password-based access.

2. **`ConfigureRedisStateComponent`**  
   Internal helper that creates and configures a “state.redis” Dapr component based on the `AzureRedisCacheResource`.  
   - Generates a valid hostname and port.  
   - Supports Azure Entra ID (AadEnabled) for secure access.  
   - Stores or references secrets in Azure Key Vault if password-based authentication is required.

3. **Secret Management**  
   - Leverages `ConfigureKeyVaultSecrets` from the main **Dapr-Azure** package to store and retrieve Redis credentials in Key Vault.  
   - Automatically adds references to the Dapr component’s metadata and secrets list, ensuring that your Redis password is managed securely.

---