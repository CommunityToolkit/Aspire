[<img src="https://raw.githubusercontent.com/dotnet-foundation/swag/master/logo/dotnetfoundation_v4.svg" alt=".NET Foundation" width=100>](https://dotnetfoundation.org)

# Aspire Community Toolkit

[![CI](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-ci.yml/badge.svg)](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-ci.yml) | [![main branch](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-main.yml/badge.svg)](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-main.yml) | [![Latest Release](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-release.yml/badge.svg)](https://github.com/CommunityToolkit/Aspire/actions/workflows/dotnet-release.yml)

The Aspire Community Toolkit is a collection of integrations and extensions for developing with Aspire.

All features are contributed by you, our amazing Aspire community, and maintained by a core set of maintainers. Check out our [FAQ](./docs/faq.md) for more information.

## 👀 What does this repo contain?

This repository contains the source code for the Aspire Community Toolkit, a collection of community created Integrations and extensions for [Aspire](https://aka.ms/dotnet/aspire).

| Package                                                                                                                                                                                                                                                                                                                                                                                      | Description                                                                                                                                                                 |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| - **Learn More**: [`Hosting.Golang`][golang-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Golang][golang-shields]][golang-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Golang][golang-shields-preview]][golang-nuget-preview]                                                                                                                   | A hosting integration Golang apps.                                                                                                                                          |
| - **Learn More**: [`Hosting.Java`][java-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Java][java-shields]][java-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Java][java-shields-preview]][java-nuget-preview]                                                                                                                                   | An integration for running Java code in .NET Aspire either using the local JDK or using a container.                                                                        |
| - **Learn More**: [`Hosting.NodeJS.Extensions`][nodejs-ext-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.NodeJS.Extensions][nodejs-ext-shields]][nodejs-ext-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.JavaScript.Extensions][nodejs-ext-shields-preview]][nodejs-ext-nuget-preview]                                                                  | An integration that contains some additional extensions for running Node.js applications                                                                                    |
| - **Learn More**: [`Hosting.Ollama`][ollama-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Ollama][ollama-shields]][ollama-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Ollama][ollama-shields-preview]][ollama-nuget-preview]                                                                                                                   | An Aspire hosting integration leveraging the [Ollama](https://ollama.com) container with support for downloading a model on startup.                                        |
| - **Learn More**: [`OllamaSharp`][ollama-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.OllamaSharp][ollamasharp-shields]][ollamasharp-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.OllamaSharp][ollamasharp-shields-preview]][ollamasharp-nuget-preview]                                                                                                        | An Aspire client integration for the [OllamaSharp](https://github.com/awaescher/OllamaSharp) package.                                                                       |
| - **Learn More**: [`Hosting.Meilisearch`][meilisearch-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Meilisearch][meilisearch-shields]][meilisearch-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Meilisearch][meilisearch-shields-preview]][meilisearch-nuget-preview]                                                                           | An Aspire hosting integration leveraging the [Meilisearch](https://meilisearch.com) container.                                                                              |
| - **Learn More**: [`Meilisearch`][meilisearch-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Meilisearch][meilisearch-client-shields]][meilisearch-client-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Meilisearch][meilisearch-client-shields-preview]][meilisearch-client-nuget-preview]                                                                       | An Aspire client integration for the [Meilisearch](https://github.com/meilisearch/meilisearch-dotnet) package.                                                              |
| - **Learn More**: [`Hosting.Azure.DataApiBuilder`][dab-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder][dab-shields]][dab-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder][dab-shields-preview]][dab-nuget-preview]                                                                                        | A hosting integration for the [Azure Data API builder](https://learn.microsoft.com/en-us/azure/data-api-builder/overview).                                                  |
| - **Learn More**: [`Hosting.Deno`][deno-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Deno][deno-shields]][deno-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Deno][deno-shields-preview]][deno-nuget-preview]                                                                                                                                   | A hosting integration for the Deno apps.                                                                                                                                    |
| - **Learn More**: [`Hosting.SqlDatabaseProjects`][sql-database-projects-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects][sql-database-projects-shields]][sql-database-projects-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects][sql-database-projects-shields-preview]][sql-database-projects-nuget-preview] | A hosting integration for the SQL Databases Projects.                                                                                                                       |
| - **Learn More**: [`Hosting.Rust`][rust-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Rust][rust-shields]][rust-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Rust][rust-shields-preview]][rust-nuget-preview]                                                                                                                                   | A hosting integration for the Rust apps.                                                                                                                                    |
| - **Learn More**: [`Hosting.Bun`][bun-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Bun][bun-shields]][bun-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Bun][bun-shields-preview]][bun-nuget-preview]                                                                                                                                           | A hosting integration for the Bun apps.                                                                                                                                     |
| - **Learn More**: [`Hosting.Python.Extensions`][python-ext-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Python.Extensions][python-ext-shields]][python-ext-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Python.Extensions][python-ext-shields-preview]][python-ext-nuget-preview]                                                                      | An integration that contains some additional extensions for running python applications                                                                                     |
| - **Learn More**: [`Hosting.KurrentDB`][kurrentdb-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.KurrentDB][kurrentdb-shields]][kurrentdb-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.KurrentDB][kurrentdb-shields-preview]][kurrentdb-nuget-preview]                                                                                           | An Aspire hosting integration leveraging the [KurrentDB](https://www.kurrent.io) container.                                                                                 |
| - **Learn More**: [`KurrentDB`][kurrentdb-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.KurrentDB][kurrentdb-client-shields]][kurrentdb-client-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.KurrentDB][kurrentdb-client-shields-preview]][kurrentdb-client-nuget-preview]                                                                                       | An Aspire client integration for the [KurrentDB](https://github.com/kurrent-io/KurrentDB-Client-Dotnet) package.                                                            |
| - **Learn More**: [`Hosting.Flagd`][flagd-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Flagd][flagd-shields]][flagd-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Flagd][flagd-shields-preview]][flagd-nuget-preview]                                                                                                                           | An Aspire hosting integration for [flagd](https://flagd.dev), a feature flag evaluation engine.                                                                         |
| - **Learn More**: [`Hosting.ActiveMQ`][activemq-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.ActiveMQ][activemq-shields]][activemq-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.ActiveMQ][activemq-shields-preview]][activemq-nuget-preview]                                                                                                   | An Aspire hosting integration leveraging the [ActiveMq](https://activemq.apache.org) container.                                                                             |
| - **Learn More**: [`Hosting.Sqlite`][sqlite-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Sqlite][sqlite-shields]][sqlite-hosting-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Sqlite][sqlite-shields-preview]][sqlite-hosting-nuget-preview]                                                                                                   | An Aspire hosting integration to setup a SQLite database with optional SQLite Web as a dev UI.                                                                              |
| - **Learn More**: [`Microsoft.Data.Sqlite`][sqlite-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Microsoft.Data.Sqlite][sqlite-shields]][sqlite-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Microsoft.Data.Sqlite][sqlite-shields-preview]][sqlite-nuget-preview]                                                                                              | An Aspire client integration for the Microsoft.Data.Sqlite NuGet package.                                                                                                   |
| - **Learn More**: [`Microsoft.EntityFrameworkCore.Sqlite`][sqlite-ef-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite][sqlite-ef-shields]][sqlite-ef-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite][sqlite-ef-shields-preview]][sqlite-ef-nuget-preview]                                  | An Aspire client integration for the Microsoft.EntityFrameworkCore.Sqlite NuGet package.                                                                                    |
| - **Learn More**: [`Hosting.Dapr`][dapr-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Dapr][dapr-shields]][dapr-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Dapr][dapr-shields-preview]][dapr-nuget-preview]                                                                                                                                   | An Aspire hosting integration for Dapr.                                                                                                                                     |
| - **Learn More**: [`Hosting.Azure.Dapr.Redis`][dapr-azureredis-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Azure.Dapr.Redis][dapr-azureredis-shields]][dapr-azureredis-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Azure.Dapr.Redis][dapr-azureredis-shields-preview]][dapr-azureredis-nuget-preview]                                        | An extension for the Dapr hosting integration for using Dapr with Azure Redis cache.                                                                                        |
| - **Learn More**: [`Hosting.RavenDB`][ravendb-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.RavenDB][ravendb-shields]][ravendb-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.RavenDB][ravendb-shields-preview]][ravendb-nuget-preview]                                                                                                           | An Aspire integration leveraging the [RavenDB](https://ravendb.net/) container.                                                                                             |
| - **Learn More**: [`RavenDB.Client`][ravendb-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.RavenDB.Client][ravendb-client-shields]][ravendb-client-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.RavenDB.Client][ravendb-client-shields-preview]][ravendb-client-nuget-preview]                                                                                  | An Aspire client integration for the [RavenDB.Client](https://www.nuget.org/packages/RavenDB.client) package.                                                               |
| - **Learn More**: [`Hosting.GoFeatureFlag`][go-feature-flag-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.GoFeatureFlag][go-feature-flag-shields]][go-feature-flag-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.GoFeatureFlag][go-feature-flag-shields-preview]][go-feature-flag-nuget-preview]                                                 | An Aspire hosting integration leveraging the [GoFeatureFlag](https://gofeatureflag.org/) container.                                                                         |
| - **Learn More**: [`GoFeatureFlag`][go-feature-flag-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.GoFeatureFlag][go-feature-flag-client-shields]][go-feature-flag-client-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.GoFeatureFlag][go-feature-flag-client-shields-preview]][go-feature-flag-client-nuget-preview]                                             | An Aspire client integration for the [GoFeatureFlag](https://github.com/open-feature/dotnet-sdk-contrib/tree/main/src/OpenFeature.Providers.GOFeatureFlag) package. |
| - **Learn More**: [`Hosting.MongoDB.Extensions`][mongodb-ext-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.MongoDB.Extensions][mongodb-ext-shields]][mongodb-ext-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.MongoDB.Extensions][mongodb-ext-shields-preview]][mongodb-ext-nuget-preview]                                                              | An integration that contains some additional extensions for hosting MongoDB container.                                                                                      |
| - **Learn More**: [`Hosting.PostgreSQL.Extensions`][postgres-ext-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.PostgreSQL.Extensions][postgres-ext-shields]][postgres-ext-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions][postgres-ext-shields-preview]][postgres-ext-nuget-preview]                                                | An integration that contains some additional extensions for hosting PostgreSQL container.                                                                                   |
| - **Learn More**: [`Hosting.Redis.Extensions`][redis-ext-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Redis.Extensions][redis-ext-shields]][redis-ext-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Redis.Extensions][redis-ext-shields-preview]][redis-ext-nuget-preview]                                                                              | An integration that contains some additional extensions for hosting Redis container.                                                                                        |
| - **Learn More**: [`Hosting.SqlServer.Extensions`][sqlserver-ext-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.SqlServer.Extensions][sqlserver-ext-shields]][sqlserver-ext-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.SqlServer.Extensions][sqlserver-ext-shields-preview]][sqlserver-ext-nuget-preview]                                              | An integration that contains some additional extensions for hosting SqlServer container.                                                                                    |
| - **Learn More**: [`Hosting.LavinMQ`][lavinmq-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.LavinMQ][lavinmq-shields]][lavinmq-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.LavinMQ][lavinmq-shields-preview]][lavinmq-nuget-preview]                                                                                                           | An Aspire hosting integration for [LavinMQ](https://www.lavinmq.com).                                                                                                       |
| - **Learn More**: [`Hosting.MailPit`][mailpit-ext-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.MailPit][mailpit-ext-shields]][mailpit-ext-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.MailPit][mailpit-ext-shields-preview]][mailpit-ext-nuget-preview]                                                                                       | An Aspire integration leveraging the [MailPit](https://mailpit.axllent.org/) container.                                                                                     |
| - **Learn More**: [`Hosting.k6`][k6-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.k6][k6-shields]][k6-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.k6][k6-shields-preview]][k6-nuget-preview]                                                                                                                                                   | An Aspire integration leveraging the [Grafana k6](https://k6.io/) container.                                                                                                |
| - **Learn More**: [`Hosting.MySql.Extensions`][mysql-ext-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.MySql.Extensions][mysql-ext-shields]][mysql-ext-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.MySql.Extensions][mysql-ext-shields-preview]][mysql-ext-nuget-preview]                                                                              | An integration that contains some additional extensions for hosting MySql container.                                                                                        |
| - **Learn More**: [`Hosting.MinIO`][minio-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Minio][minio-hosting-shields]][minio-hosting-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Minio][minio-hosting-shields-preview]][minio-hosting-nuget-preview]                                                                                           | An Aspire hosting integration to setup a [MinIO S3](https://min.io/) storage.                                                                                               |
| - **Learn More**: [`MinIO.Client`][minio-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Minio.Client][minio-client-shields]][minio-client-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Client.Minio][minio-client-shields-preview]][minio-client-nuget-preview]                                                                                                  | An Aspire client integration for the [MinIO](https://github.com/minio/minio-dotnet) package.                                                                                |
| - **Learn More**: [`Hosting.SurrealDb`][surrealdb-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.SurrealDb][surrealdb-shields]][surrealdb-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.SurrealDb][surrealdb-shields-preview]][surrealdb-nuget-preview]                                                                                           | An Aspire hosting integration leveraging the [SurrealDB](https://surrealdb.com/) container.                                                                                 |
| - **Learn More**: [`SurrealDb`][surrealdb-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.SurrealDb][surrealdb-client-shields]][surrealdb-client-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.SurrealDb][surrealdb-client-shields-preview]][surrealdb-client-nuget-preview]                                                                                       | An Aspire client integration for the [SurrealDB](https://github.com/surrealdb/surrealdb.net/) package.                                                                      |
| - **Learn More**: [`Hosting.Elasticsearch.Extensions`][elasticsearch-ext-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions][elasticsearch-ext-shields]][elasticsearch-ext-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions][elasticsearch-ext-shields-preview]][elasticsearch-ext-nuget-preview]      | An integration that contains some additional extensions for hosting Elasticsearch container.                                                                                |
| - **Learn More**: [`Hosting.Umami`][umami-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Hosting.Umami][umami-shields]][umami-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Umami][umami-shields-preview]][umami-nuget-preview]                                                                                           | An Aspire hosting integration leveraging the [Umami](https://umami.is/) container.                                                                                 |
| - **Learn More**: [`Hosting.Azure.Extensions`][azure-ext-integration-docs] <br /> - Stable 📦: [![CommunityToolkit.Aspire.Azure.Extensions][azure-ext-shields]][azure-ext-nuget] <br /> - Preview 📦: [![CommunityToolkit.Aspire.Hosting.Azure.Extensions][azure-ext-shields-preview]][azure-ext-nuget-preview]                                              | An integration that contains some additional extensions for hosting Azure container.                                                                                    |

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

Documentation for the Aspire Community Toolkit is available on the [Microsoft Docs](https://learn.microsoft.com/dotnet/aspire/community-toolkit/overview).

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
[nodejs-ext-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.JavaScript.Extensions
[nodejs-ext-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.JavaScript.Extensions/
[nodejs-ext-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.JavaScript.Extensions?label=nuget%20(preview)
[nodejs-ext-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.JavaScript.Extensions/absoluteLatest
[ollama-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/ollama
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
[dab-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-data-api-builder
[dab-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder
[dab-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder/
[dab-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder?label=nuget%20(preview)
[dab-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder/absoluteLatest
[deno-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-deno
[deno-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Deno
[deno-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Deno/
[deno-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Deno?label=nuget%20(preview)
[deno-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Deno/absoluteLatest
[sql-database-projects-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-sql-database-projects
[sql-database-projects-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects
[sql-database-projects-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects/
[sql-database-projects-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects?label=nuget%20(preview)
[sql-database-projects-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects/absoluteLatest
[rust-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-rust
[rust-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Rust
[rust-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Rust/
[rust-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Rust?label=nuget%20(preview)
[rust-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Rust/absoluteLatest
[bun-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-bun
[bun-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Bun
[bun-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Bun/
[bun-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Bun?label=nuget%20(preview)
[bun-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Bun/absoluteLatest
[python-ext-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-python-extensions
[python-ext-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Python.Extensions
[python-ext-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Python.Extensions/
[python-ext-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Python.Extensions?label=nuget%20(preview)
[python-ext-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Python.Extensions/absoluteLatest
[kurrentdb-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-kurrentdb
[kurrentdb-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.KurrentDB
[kurrentdb-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.KurrentDB/
[kurrentdb-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.KurrentDB?label=nuget%20(preview)
[kurrentdb-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.KurrentDB/absoluteLatest
[kurrentdb-client-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.KurrentDB
[kurrentdb-client-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.KurrentDB/
[kurrentdb-client-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.KurrentDB?label=nuget%20(preview)
[kurrentdb-client-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.KurrentDB/absoluteLatest
[flagd-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-flagd
[flagd-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Flagd
[flagd-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Flagd/
[flagd-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Flagd?label=nuget%20(preview)
[flagd-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Flagd/absoluteLatest
[activemq-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-activemq
[activemq-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.ActiveMQ
[activemq-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.ActiveMQ/
[activemq-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.ActiveMQ?label=nuget%20(preview)
[activemq-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.ActiveMQ/absoluteLatest
[sqlite-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/sqlite
[sqlite-hosting-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Sqlite
[sqlite-hosting-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Sqlite/
[sqlite-hosting-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Sqlite?label=nuget%20(preview)
[sqlite-hosting-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Sqlite/absoluteLatest
[sqlite-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Microsoft.Data.Sqlite
[sqlite-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Microsoft.Data.Sqlite/
[sqlite-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Microsoft.Data.Sqlite?label=nuget%20(preview)
[sqlite-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Microsoft.Data.Sqlite/absoluteLatest
[sqlite-ef-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/sqlite-entity-framework-integration
[sqlite-ef-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite
[sqlite-ef-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite/
[sqlite-ef-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite?label=nuget%20(preview)
[sqlite-ef-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Microsoft.EntityFrameworkCore.Sqlite/absoluteLatest
[dapr-integration-docs]: https://learn.microsoft.com/dotnet/aspire/frameworks/dapr
[dapr-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Dapr
[dapr-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Dapr/
[dapr-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Dapr?label=nuget%20(preview)
[dapr-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Dapr/absoluteLatest
[dapr-azureredis-integration-docs]: https://learn.microsoft.com/dotnet/aspire/frameworks/dapr
[dapr-azureredis-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Azure.Dapr.Redis
[dapr-azureredis-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Azure.Dapr.Redis/
[dapr-azureredis-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Azure.Dapr.Redis?label=nuget%20(preview)
[dapr-azureredis-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Azure.Dapr.Redis/absoluteLatest
[ravendb-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/ravendb
[ravendb-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.RavenDB
[ravendb-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.RavenDB/
[ravendb-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.RavenDB?label=nuget%20(preview)
[ravendb-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.RavenDB/absoluteLatest
[ravendb-client-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.RavenDB.Client
[ravendb-client-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.RavenDB.Client/
[ravendb-client-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.RavenDB.Client?label=nuget%20(preview)
[ravendb-client-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.RavenDB.Client/absoluteLatest
[go-feature-flag-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-go-feature-flag
[go-feature-flag-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.GoFeatureFlag
[go-feature-flag-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.GoFeatureFlag/
[go-feature-flag-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.GoFeatureFlag?label=nuget%20(preview)
[go-feature-flag-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.GoFeatureFlag/absoluteLatest
[go-feature-flag-client-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.GoFeatureFlag
[go-feature-flag-client-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.GoFeatureFlag/
[go-feature-flag-client-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.GoFeatureFlag?label=nuget%20(preview)
[go-feature-flag-client-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.GoFeatureFlag/absoluteLatest
[mongodb-ext-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-mongodb-extensions
[mongodb-ext-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.MongoDB.Extensions
[mongodb-ext-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.MongoDB.Extensions/
[mongodb-ext-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.MongoDB.Extensions?label=nuget%20(preview)
[mongodb-ext-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.MongoDB.Extensions/absoluteLatest
[postgres-ext-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-postgresql-extensions
[postgres-ext-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions
[postgres-ext-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions/
[postgres-ext-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions?label=nuget%20(preview)
[postgres-ext-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions/absoluteLatest
[sqlserver-ext-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-sqlserver-extensions
[sqlserver-ext-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.SqlServer.Extensions
[sqlserver-ext-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.SqlServer.Extensions/
[sqlserver-ext-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.SqlServer.Extensions?label=nuget%20(preview)
[sqlserver-ext-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.SqlServer.Extensions/absoluteLatest
[redis-ext-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-redis-extensions
[redis-ext-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Redis.Extensions
[redis-ext-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Redis.Extensions/
[redis-ext-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Redis.Extensions?label=nuget%20(preview)
[redis-ext-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Redis.Extensions/absoluteLatest
[lavinmq-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-lavinmq
[lavinmq-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.LavinMQ
[lavinmq-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.LavinMQ/
[lavinmq-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.LavinMQ?label=nuget%20(preview)
[lavinmq-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.LavinMQ/absoluteLatest
[mailpit-ext-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-mailpit
[mailpit-ext-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.MailPit
[mailpit-ext-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.MailPit/
[mailpit-ext-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.MailPit?label=nuget%20(preview)
[mailpit-ext-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.MailPit/absoluteLatest
[k6-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-k6
[k6-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.k6
[k6-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.k6/
[k6-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.k6?label=nuget%20(preview)
[k6-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.k6/absoluteLatest
[mysql-ext-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-mysql-extensions
[mysql-ext-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.MySql.Extensions
[mysql-ext-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.MySql.Extensions/
[mysql-ext-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.MySql.Extensions?label=nuget%20(preview)
[mysql-ext-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.MySql.Extensions/absoluteLatest
[minio-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-minio
[minio-hosting-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Minio
[minio-hosting-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Minio/
[minio-hosting-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Minio?label=nuget%20(preview)
[minio-hosting-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Minio/absoluteLatest
[minio-client-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Minio.Client
[minio-client-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Minio.Client/
[minio-client-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Minio.Client?label=nuget%20(preview)
[minio-client-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Minio.Client/absoluteLatest
[surrealdb-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-surrealdb
[surrealdb-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.SurrealDb
[surrealdb-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.SurrealDb/
[surrealdb-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.SurrealDb?label=nuget%20(preview)
[surrealdb-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.SurrealDb/absoluteLatest
[surrealdb-client-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.SurrealDb
[surrealdb-client-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.SurrealDb/
[surrealdb-client-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.SurrealDb?label=nuget%20(preview)
[surrealdb-client-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.SurrealDb/absoluteLatest
[elasticsearch-ext-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-elasticsearch-extensions
[elasticsearch-ext-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions
[elasticsearch-ext-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions/
[elasticsearch-ext-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions?label=nuget%20(preview)
[elasticsearch-ext-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions/absoluteLatest
[umami-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-umami
[umami-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Umami
[umami-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Umami/
[umami-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Umami?label=nuget%20(preview)
[umami-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Umami/absoluteLatest
[azure-ext-integration-docs]: https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-azure-extensions
[azure-ext-shields]: https://img.shields.io/nuget/v/CommunityToolkit.Aspire.Hosting.Azure.Extensions
[azure-ext-nuget]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Azure.Extensions/
[azure-ext-shields-preview]: https://img.shields.io/nuget/vpre/CommunityToolkit.Aspire.Hosting.Azure.Extensions?label=nuget%20(preview)
[azure-ext-nuget-preview]: https://nuget.org/packages/CommunityToolkit.Aspire.Hosting.Azure.Extensions/absoluteLatest
