[<img src="https://raw.githubusercontent.com/dotnet-foundation/swag/master/logo/dotnetfoundation_v4.svg" alt=".NET Foundation" width=100>](https://dotnetfoundation.org)

# .NET Aspire Community Toolkit

[![CI](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-ci.yml/badge.svg)](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-ci.yml) | [![main branch](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-main.yml/badge.svg)](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-main.yml) | [![Latest Release](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-release.yml/badge.svg)](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-release.yml)

The .NET Aspire Community Toolkit is a collection of common components and extensions for development with .NET Aspire.

All features are contributed by you, our amazing .NET community, and maintained by a core set of maintainers.

## 👀 What does this repo contain?

This repository contains the source code for the .NET Aspire Community Toolkit, a collection of community created Integrations and extensions for [.NET Aspire](https://aka.ms/dotnet/aspire).

| Package | Description |
| - **Learn More**: [`Hosting.Azure.StaticWebApps`][swa-integration-docs] <br /> - Stable 📦: [![Aspire.CommunityToolkit.Hosting.Azure.StaticWebApps][swa-shields]][swa-nuget] <br /> - Preview 📦: [![Aspire.CommunityToolkit.Hosting.Azure.StaticWebApps][swa-shields-preview]][swa-nuget-preview] | A hosting integration for the [Azure Static Web Apps emulator](https://learn.microsoft.com/azure/static-web-apps/static-web-apps-cli-overview) (Note: this does not support deployment of a project to Azure Static Web Apps). |
| **Learn More**: [`Hosting.Java`][java-integration-docs] <br /> - Stable 📦: [![Aspire.CommunityToolkit.Hosting.Java][java-shields]][java-nuget] <br /> - Preview 📦: [![Aspire.CommunityToolkit.Hosting.Java][java-shields-preview]][java-nuget-preview] | A integration for running Java code in .NET Aspire either using the local JDK or using a container. |
| - **Learn More**: [`Hosting.NodeJS.Extensions`][nodejs-ext-integration-docs] <br /> - Stable 📦: [![Aspire.CommunityToolkit.NodeJS.Extensions][nodejs-ext-shields]][nodejs-ext-nuget] <br /> - Preview 📦: [![Aspire.CommunityToolkit.Hosting.NodeJS.Extensions][nodejs-ext-shields-preview]][nodejs-ext-nuget-preview] | An integration that contains some additional extensions for running Node.js applications |
| - **Learn More**: [`Hosting.Ollama`][ollama-integration-docs] <br /> - Stable 📦: [![Aspire.CommunityToolkit.Ollama][ollama-shields]][ollama-nuget] <br /> - Preview 📦: [![Aspire.CommunityToolkit.Hosting.Ollama][ollama-shields-preview]][ollama-nuget-preview] | An Aspire component leveraging the [Ollama](https://ollama.com) container with support for downloading a model on startup. |

## 🙌 Getting Started

Each of the integrations in the toolkit is available as a NuGet package, and can be added to your .NET project. Refer to the table above for the available integrations and the documentation on how to use them.

## 📃 Documentation

Documentation for the .NET Aspire Community Toolkit is available on the [GitHub Pages site](https://learn.microsoft.com/dotnet/communitytoolkit/aspire/).

## 🚀 Contribution

Do you want to contribute?

Check out our [Contributing guide](./CONTRIBUTING.md) to learn more about contribution and guidelines!

## 🏆 Contributors

[![Toolkit Contributors](https://contrib.rocks/image?repo=CommunityToolkit/aspire)](https://github.com/CommunityToolkit/aspire/graphs/contributors)

Made with [contrib.rocks](https://contrib.rocks).

## Code of Conduct

As a part of the .NET Foundation, we have adopted the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct). Please familiarize yourself with that before participating with this repository. Thanks!

## .NET Foundation

This project is supported by the [.NET Foundation](https://dotnetfoundation.org).

[swa-integration-docs]: https://learn.microsoft.com/dotnet/communitytoolkit/aspire/integrations/hosting-azure-static-web-apps
[swa-shields]: https://img.shields.io/nuget/v/Aspire.CommunityToolkit.Hosting.Azure.StaticWebApps
[swa-nuget]: https://nuget.org/packages/Aspire.CommunityToolkit.Hosting.Azure.StaticWebApps/
[swa-shields-preview]: https://img.shields.io/nuget/v/Aspire.CommunityToolkit.Hosting.Azure.StaticWebApps?label=nuget%20(preview)
[swa-nuget-preview]: https://nuget.org/packages/Aspire.CommunityToolkit.Hosting.Azure.StaticWebApps/absoluteLatest
[java-integration-docs]: https://learn.microsoft.com/dotnet/communitytoolkit/aspire/integrations/hosting-java
[java-shields]: https://img.shields.io/nuget/v/Aspire.CommunityToolkit.Hosting.Java
[java-nuget]: https://nuget.org/packages/Aspire.CommunityToolkit.Hosting.Java/
[java-shields-preview]: https://img.shields.io/nuget/v/Aspire.CommunityToolkit.Hosting.Java?label=nuget%20(preview)
[java-nuget-preview]: https://nuget.org/packages/Aspire.CommunityToolkit.Hosting.Java/absoluteLatest
[nodejs-ext-integration-docs]: https://learn.microsoft.com/dotnet/communitytoolkit/aspire/integrations/hosting-nodejs-extensions
[nodejs-ext-shields]: https://img.shields.io/nuget/v/Aspire.CommunityToolkit.Hosting.NodeJS.Extensions
[nodejs-ext-nuget]: https://nuget.org/packages/Aspire.CommunityToolkit.Hosting.NodeJS.Extensions/
[nodejs-ext-shields-preview]: https://img.shields.io/nuget/v/Aspire.CommunityToolkit.Hosting.NodeJS.Extensions?label=nuget%20(preview)
[nodejs-ext-nuget-preview]: https://nuget.org/packages/Aspire.CommunityToolkit.Hosting.NodeJS.Extensions/absoluteLatest
[ollama-integration-docs]: https://learn.microsoft.com/dotnet/communitytoolkit/aspire/integrations/hosting-ollama
[ollama-shields]: https://img.shields.io/nuget/v/Aspire.CommunityToolkit.Hosting.Ollama
[ollama-nuget]: https://nuget.org/packages/Aspire.CommunityToolkit.Hosting.Ollama/
[ollama-shields-preview]: https://img.shields.io/nuget/v/Aspire.CommunityToolkit.Hosting.Ollama?label=nuget%20(preview)
[ollama-nuget-preview]: https://nuget.org/packages/Aspire.CommunityToolkit.Hosting.Ollama/absoluteLatest
