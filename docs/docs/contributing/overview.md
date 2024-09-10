Anyone can contribute to the .NET Aspire Community Toolkit and before you get started, be sure to read the [Contributing Guide](../../../CONTRIBUTING.md) to learn how to contribute to the project. In this section, we will expand a bit deeper on to the expectations and guidelines for contributing a new integration.

## Prerequisites

Development against the .NET Aspire Community Toolkit is recommended to be done using the provided [devcontainer](https://containers.dev) using Visual Studio Code and the [Remote Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers). This will ensure that you have all the necessary tools and dependencies installed to build and test the toolkit.

### Manual Setup

If you prefer not to use the devcontainer, you can manually set up your development environment by installing the following tools:

-   [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
-   [Node.js](https://nodejs.org/download/)
-   [Azure Static Web Apps CLI](https://learn.microsoft.com/azure/static-web-apps/local-development)
-   [OpenJDK 21](https://learn.microsoft.com/java/openjdk/download/)
-   [Docker](https://www.docker.com/products/docker-desktop) or [Podman](https://podman.io/)

## Repository Structure

There are four main directories in the repository:

-  `src`: Contains the source code for the toolkit.
-  `docs`: Contains the documentation for the toolkit.
-  `examples`: Contains sample applications that demonstrate how to use the toolkit.
-  `tests`: Contains the test projects for the toolkit.

## Contributing a New Integration

There are three 