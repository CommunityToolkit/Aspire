# CommunityToolkit.Hosting.Azure.StaticWebApps

<!-- Badges go here -->

## Overview

This is a .NET Aspire Integration for using the [Azure Static Web App CLI](https://learn.microsoft.com/azure/static-web-apps/local-development) to run Azure Static Web Apps locally using the emulator.

It provides support for proxying both the static frontend and the API backend using resources defined in the AppHost project.

> [!NOTE]
> This does not support deployment to Azure Static Web Apps.

## Usage

> [!NOTE]
> This integration requires the Azure Static Web Apps CLI to be installed. You can install it using the following command:
> `npm install -g @azure/static-web-apps-cli`

[!code-csharp[](../../../examples/swa/CommunityToolkit.Aspire.StaticWebApps.AppHost/Program.cs)]

### Configuration

-   `Port` - The port to run the emulator on. Defaults to `4280`.
-   `DevServerTimeout` - The timeout (in seconds) for the frontend dev server. Defaults to `60`.
