# .NET Aspire Community Toolkit

[![CI](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-ci.yml/badge.svg)](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-ci.yml) | [![main branch](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-main.yml/badge.svg)](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-main.yml) | [![Latest Release](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-release.yml/badge.svg)](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-release.yml)

The .NET Aspire Community Toolkit is a collection of common components and extensions for development with .NET Aspire.

All features are contributed by you, our amazing .NET community, and maintained by a core set of maintainers.

## 👀 What does this repo contain?

This repository contains the source code for the .NET Aspire Community Toolkit, a collection of community created Integrations and extensions for [.NET Aspire](https://aka.ms/dotnet/aspire).

| Package                                                    | Latest stable                                                                        | Latest Preview                                                                                               | Description                                                                                                                                                                                                                    |
| ---------------------------------------------------------- | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| [`Hosting.Azure.StaticWebApps`][swa-integration-docs]      | [![CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps][swa-shields]][swa-nuget]     | [![CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps][swa-shields-preview]][swa-nuget-preview]             | A hosting integration for the [Azure Static Web Apps emulator](https://learn.microsoft.com/azure/static-web-apps/static-web-apps-cli-overview) (Note: this does not support deployment of a project to Azure Static Web Apps). |
| [`Hosting.Java`][java-integration-docs]                    | [![CommunityToolkit.Aspire.Hosting.Java][java-shields]][java-nuget]                  | [![CommunityToolkit.Aspire.Hosting.Java][java-shields-preview]][java-nuget-preview]                          | A integration for running Java code in .NET Aspire either using the local JDK or using a container.                                                                                                                            |
| [`Hosting.NodeJS.Extensions`][nodejs-ext-integration-docs] | [![CommunityToolkit.Aspire.NodeJS.Extensions][nodejs-ext-shields]][nodejs-ext-nuget] | [![CommunityToolkit.Aspire.Hosting.NodeJS.Extensions][nodejs-ext-shields-preview]][nodejs-ext-nuget-preview] | An integration that contains some additional extensions for running Node.js applications                                                                                                                                       |
| [`Hosting.Ollama`][ollama-integration-docs]                | [![CommunityToolkit.Aspire.Ollama][ollama-shields]][ollama-nuget]                    | [![CommunityToolkit.Aspire.Hosting.Ollama][ollama-shields-preview]][ollama-nuget-preview]                    | An Aspire component leveraging the [Ollama](https://ollama.com) container with support for downloading a model on startup.                                                                                                     |

## 🙌 Getting Started

Each of the integrations in the toolkit is available as a NuGet package, and can be added to your .NET project. Refer to the table above for the available integrations and the documentation on how to use them.

## 📃 Documentation

Documentation for the .NET Aspire Community Toolkit is available on the [GitHub Pages site](https://communitytoolkit.github.io/aspire/).

## 🚀 Contribution

Do you want to contribute?

Check out our [Contributing guide](./CONTRIBUTING.md) to learn more about contribution and guidelines!

## 🏆 Contributors

[![Toolkit Contributors](https://contrib.rocks/image?repo=CommunityToolkit/aspire)](https://github.com/CommunityToolkit/aspire/graphs/contributors)

Made with [contrib.rocks](https://contrib.rocks).

[swa-integration-docs]: https://communitytoolkit.github.io/aspire/integrations/hosting-azure-static-web-apps
[swa-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps
[swa-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps/
[swa-shields-preview]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps?label=nuget%20(preview)
[swa-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps/absoluteLatest
[java-integration-docs]: https://communitytoolkit.github.io/aspire/integrations/hosting-java
[java-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Java
[java-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Java/
[java-shields-preview]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Java?label=nuget%20(preview)
[java-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Java/absoluteLatest
[nodejs-ext-integration-docs]: https://communitytoolkit.github.io/aspire/integrations/hosting-nodejs-extensions
[nodejs-ext-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions
[nodejs-ext-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions/
[nodejs-ext-shields-preview]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions?label=nuget%20(preview)
[nodejs-ext-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions/absoluteLatest
[ollama-integration-docs]: https://communitytoolkit.github.io/aspire/integrations/hosting-ollama
[ollama-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Ollama
[ollama-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Ollama/
[ollama-shields-preview]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Ollama?label=nuget%20(preview)
[ollama-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Ollama/absoluteLatest
