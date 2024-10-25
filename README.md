[<img src="https://raw.githubusercontent.com/dotnet-foundation/swag/master/logo/dotnetfoundation_v4.svg" alt=".NET Foundation" width=100>](https://dotnetfoundation.org)

# .NET Aspire Community Toolkit

[![CI](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-ci.yml/badge.svg)](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-ci.yml) | [![main branch](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-main.yml/badge.svg)](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-main.yml) | [![Latest Release](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-release.yml/badge.svg)](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-release.yml)

The .NET Aspire Community Toolkit is a collection of integrations and extensions for developing with .NET Aspire.

All features are contributed by you, our amazing .NET community, and maintained by a core set of maintainers. Check out our [FAQ](./docs/faq.md) for more information.

## 👀 What does this repo contain?

This repository contains the source code for the .NET Aspire Community Toolkit, a collection of community created Integrations and extensions for [.NET Aspire](https://aka.ms/dotnet/aspire).

| Package                                                                                                                                                                                                                                                                                                                 | Description                                                                                                                                                                                                                    |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| - **Learn More**: [`Hosting.Azure.StaticWebApps`][swa-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps][swa-shields]][swa-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps][swa-shields-preview]][swa-nuget-preview]                      | A hosting integration for the [Azure Static Web Apps emulator](https://learn.microsoft.com/azure/static-web-apps/static-web-apps-cli-overview) (Note: this does not support deployment of a project to Azure Static Web Apps). |
| - **Learn More**: [`Hosting.Golang`][golang-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Golang][golang-shields]][golang-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Golang][golang-shields-preview]][golang-nuget-preview]                                              | A hosting integration Golang apps.                                                                                                                                                                                             |
| **Learn More**: [`Hosting.Java`][java-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Java][java-shields]][java-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Java][java-shields-preview]][java-nuget-preview]                                                                | A integration for running Java code in .NET Aspire either using the local JDK or using a container.                                                                                                                            |
| - **Learn More**: [`Hosting.NodeJS.Extensions`][nodejs-ext-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.NodeJS.Extensions][nodejs-ext-shields]][nodejs-ext-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.NodeJS.Extensions][nodejs-ext-shields-preview]][nodejs-ext-nuget-preview] | An integration that contains some additional extensions for running Node.js applications                                                                                                                                       |
| - **Learn More**: [`Hosting.Ollama`][ollama-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Ollama][ollama-shields]][ollama-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Ollama][ollama-shields-preview]][ollama-nuget-preview]                                              | An Aspire hosting integration leveraging the [Ollama](https://ollama.com) container with support for downloading a model on startup.                                                                                           |
| - **Learn More**: [`OllamaSharp`][ollama-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.OllamaSharp][ollamasharp-shields]][ollamasharp-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.OllamaSharp][ollamasharp-shields-preview]][ollama-nuget-preview]                                        | An Aspire client integration for the [OllamaSharp](https://github.com/awaescher/OllamaSharp) package.                                                                                                                          |
| - **Learn More**: [`Hosting.Meilisearch`][meilisearch-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Meilisearch][meilisearch-shields]][meilisearch-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Meilisearch][meilisearch-shields-preview]][meilisearch-nuget-preview]      | An Aspire hosting integration leveraging the [Meilisearch](https://meilisearch.com) container.                                                                                                                                 |
| - **Learn More**: [`Meilisearch`][meilisearch-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Meilisearch][meilisearch-client-shields]][meilisearch-client-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Meilisearch][meilisearch-client-shields-preview]][meilisearch-client-nuget-preview]  | An Aspire client integration for the [Meilisearch](https://github.com/meilisearch/meilisearch-dotnet) package.                                                                                                                 |

## 🙌 Getting Started

Each of the integrations in the toolkit is available as a NuGet package, and can be added to your .NET project. Refer to the table above for the available integrations and the documentation on how to use them.

### 📦 Installation

Stable releases of the NuGet packages will be published to the [NuGet Gallery](https://www.nuget.org/packages?q=CommunityToolkit.Aspire). For pre-release versions, you can use Azure Artifacts feeds:

-   [View latest `main` branch packages](https://dev.azure.com/dotnet/CommunityToolkit/_packaging?_a=feed&feed=CommunityToolkit-MainLatest)
    -   [Feed URL](https://pkgs.dev.azure.com/dotnet/CommunityToolkit/_packaging/CommunityToolkit-MainLatest/nuget/v3/index.json)
-   [View latest PR build packages](https://dev.azure.com/dotnet/CommunityToolkit/_packaging?_a=feed&feed=CommunityToolkit-PullRequests)
    -   [Feed URL](https://pkgs.dev.azure.com/dotnet/CommunityToolkit/_packaging/CommunityToolkit-PullRequests/nuget/v3/index.json)

_Stable releases are not published to the Azure Artifacts feeds, they can only be accessed from the NuGet Gallery._

## 📃 Documentation

Documentation for the .NET Aspire Community Toolkit is available on the [Microsoft Docs](https://learn.microsoft.com/dotnet/aspire/community-toolkit/overview).

## 🚀 Contribution

Do you want to contribute?

Check out our [Contributing guide](./CONTRIBUTING.md) to learn more about contribution and guidelines!

## 🏆 Contributors

[![Toolkit Contributors](https://contrib.rocks/image?repo=CommunityToolkit/Aspire)](https://github.com/CommunityToolkit/Aspire/graphs/contributors)

Made with [contrib.rocks](https://contrib.rocks).

## Code of Conduct

As a part of the .NET Foundation, we have adopted the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct). Please familiarize yourself with that before participating with this repository. Thanks!

## .NET Foundation

This project is supported by the [.NET Foundation](https://dotnetfoundation.org).

[swa-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-azure-static-web-apps
[swa-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps
[swa-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps/
[swa-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps?label=nuget%20(preview)
[swa-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps/absoluteLatest
[golang-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-golang
[golang-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Golang
[golang-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Golang/
[golang-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Golang?label=nuget%20(preview)
[golang-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Golang/absoluteLatest
[java-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-java
[java-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Java
[java-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Java/
[java-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Java?label=nuget%20(preview)
[java-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Java/absoluteLatest
[nodejs-ext-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-nodejs-extensions
[nodejs-ext-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions
[nodejs-ext-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions/
[nodejs-ext-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions?label=nuget%20(preview)
[nodejs-ext-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions/absoluteLatest
[ollama-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-ollama
[ollama-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Ollama
[ollama-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Ollama/
[ollama-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Ollama?label=nuget%20(preview)
[ollama-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Ollama/absoluteLatest
[ollamasharp-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.OllamaSharp
[ollamasharp-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.OllamaSharp/
[ollamasharp-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.OllamaSharp?label=nuget%20(preview)
[ollamasharp-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.OllamaSharp/absoluteLatest
[meilisearch-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-meilisearch
[meilisearch-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Meilisearch
[meilisearch-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Meilisearch/
[meilisearch-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Meilisearch?label=nuget%20(preview)
[meilisearch-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Meilisearch/absoluteLatest
[meilisearch-client-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Meilisearch
[meilisearch-client-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Meilisearch/
[meilisearch-client-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Meilisearch?label=nuget%20(preview)
[meilisearch-client-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Meilisearch/absoluteLatest

