# CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps library

**Deprecation warning**: This library is deprecated and will be removed in a future release, refer to https://github.com/CommunityToolkit/Aspire/issues/698 for more information.

Provides extensions methods and resource definitions for the .NET Aspire AppHost to support running Azure Static Web Apps locally using the emulator using the [Azure Static Web App CLI](https://learn.microsoft.com/azure/static-web-apps/local-development).

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a frontend and backend resource (optional), then call `AddSwaEmulator`:

```csharp
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

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-azure-static-web-apps

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
