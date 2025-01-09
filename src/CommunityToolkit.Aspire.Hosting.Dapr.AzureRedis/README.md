# CommunityToolkit.Aspire.Hosting.Dapr.AzureRedis

This package extends [CommunityToolkit.Aspire.Hosting.Dapr.Azure] by adding specialized integration for **Azure Redis** as a Dapr state store in Aspire Hosting applications.

It provides a convenient way to configure an **Azure Redis** resource so that Dapr components (specifically, a Redis state store) can integrate smoothly into the Aspire deployment pipeline.

---

## Features
   - Automatically sets up parameters for **Redis Host** and handles **TLS** configuration.  
   - Integrates secret management if Azure Redis requires password-based access.
   - Generates a valid hostname and port.  
   - Supports Azure Entra ID (AadEnabled) for secure access.  
   - Stores or references secrets in Azure Key Vault if password-based authentication is required.
   - Automatically adds references to the Dapr component’s metadata and secrets list.

---