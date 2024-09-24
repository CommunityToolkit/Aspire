Anyone can contribute to the .NET Aspire Community Toolkit. Before you get started, be sure to read the [Contributing Guide](https://github.com/CommunityToolkit/aspire/tree/main/CONTRIBUTING.md) to learn how to contribute to the project. In this section, we will expand a bit deeper on to the expectations and guidelines for contributing a new integration.

## Prerequisites

Development against the .NET Aspire Community Toolkit is recommended to be done using the provided [devcontainer](https://containers.dev) using Visual Studio Code and the [Remote Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers). This will ensure that you have all the necessary tools and dependencies installed to build and test the toolkit.

### Manual Setup

If you prefer not to use the devcontainer, you can manually set up your development environment by installing the following tools:

-   [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
-   [Node.js](https://nodejs.org/download/)
-   [Azure Static Web Apps CLI](https://learn.microsoft.com/azure/static-web-apps/local-development)
-   [OpenJDK 21](https://learn.microsoft.com/java/openjdk/download/)
-   [Docker](https://www.docker.com/products/docker-desktop) or [Podman](https://podman.io/)
-   [Python](https://www.python.org/downloads/) (needed for docs)

## Repository Structure

There are four main directories in the repository:

-   `src`: Contains the source code for the toolkit.
-   `docs`: Contains the documentation for the toolkit.
-   `examples`: Contains sample applications that demonstrate how to use the toolkit.
-   `tests`: Contains the test projects for the toolkit.

## Contributing a New Integration

### Naming Convention

When creating a new integration, the project should be prefixed with `Aspire.CommunityToolkit` and the name should then follow a similar design as the .NET Aspire integrations. For example, with the `Aspire.CommunityToolkit.Hosting.Azure.StaticWebApps` integration, the integration is a hosting integration (meaning it's used in the AppHost project), it's specific for Azure, and related to the Static Web Apps service. In contrast, the `Aspire.CommunityToolkit.Hosting.Java` integration is a hosting integration, but it's specific for Java applications but not related to any specific cloud provider.

### Tests

All new integrations should have a test project that covers the integration. The test project should be named `Aspire.CommunityToolkit.Hosting.<Integration>.Tests` and should be located in the `tests` directory. The test project should contain both unit and integration tests. The testing framework of choice is xUnit and will be automatically added to the test project.

For unit tests, it should cover all the configuration parameters that are available for configuring the integration on the AppHost.

Integration tests should use the sample application that is provided for the integration and launch AppHost project using the [.NET Aspire testing framework](https://learn.microsoft.com/dotnet/aspire/fundamentals/testing?pivots=xunit). To simplify this, an [xUnit collection feature](https://xunit.net/docs/shared-context#collection-fixture), `AspireIntegrationTestFixture<TAppHostProject>` has been created in the `Aspire.CommunityToolkit.Testing` project that will launch the AppHost project once for all the tests in the collection.

Here is an example of an integration test:

```csharp
public class SwaHostingComponentTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_StaticWebApps_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_StaticWebApps_AppHost>> {
    // Write tests here
}
```

#### Test Helpers

The `Aspire.CommunityToolkit.Testing` project contains some helpers for writing tests.

-   `WaitForTextAsync`: A helper that waits for a specific text to appear in the logs of the resource started by the AppHost project.

### Sample Project

A sample project should be created in the `examples` directory that demonstrates how to use the integration. This should consist of an AppHost project that uses the integration and any other projects that are useful to demonstrate the usage of the integration.

### Documentation

All new integrations should have documentation that explains how to use the integration. The documentation should be located in the `docs` directory and should be named `integrations/<integration>.md`. The documentation should include the following sections:

-   Overview: A brief overview of the integration.
-   Configuration: A list of all the configuration parameters that are available for the integration.
-   Usage: A guide on how to use the integration in an AppHost project.

