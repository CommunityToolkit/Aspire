# CommunityToolkit.Hosting.Azure.StaticWebApps

<!-- Badges go here -->

## Overview

This is a .NET Aspire Integration for using the [Azure Static Web App CLI](https://learn.microsoft.com/azure/static-web-apps/local-development) to run Azure Static Web Apps locally using the emulator.

It provides support for proxying both the static frontend and the API backend using resources defined in the AppHost project.

!!! note
    This does not support deployment to Azure Static Web Apps.

## Usage

!!! note
    This integration requires the Azure Static Web Apps CLI to be installed. You can install it using the following command:

    ```bash
    npm install -g @azure/static-web-apps-cli
    ```

```csharp
using CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps;

var builder = DistributedApplication.CreateBuilder(args);

// Define the API resource
var api = builder.AddProject<Projects.CommunityToolkit_Aspire_StaticWebApps_ApiApp>("api");

// Define the frontend resource
var web = builder
    .AddNpmApp("web", Path.Combine("..", "CommunityToolkit.Aspire.StaticWebApps.WebApp"), "dev")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

// Create a SWA emulator with the frontend and API resources
_ = builder
    .AddSwaEmulator("swa")
    .WithAppResource(web)
    .WithApiResource(api);

builder.Build().Run();
```

### Configuration

-   `Port` - The port to run the emulator on. Defaults to `4280`.
-   `DevServerTimeout` - The timeout (in seconds) for the frontend dev server. Defaults to `60`.
