# 🛠️ Setting up your Development Environment

Because the Community Toolkit consists of integrations across a lot of different runtimes, setting up a development environment can be a bit complex. Below we outline the recommended approach to developing, as well as a manual setup if you prefer not to use the recommended approach.

## ✅ Recommended Setup

The easiest, and recommended way is to use [VS Code](https://code.visualstudio.com/) with the [`devcontainer`](https://code.visualstudio.com/docs/remote/containers) extension.

This will run the development environment in a container, install all the necessary tools and dependencies, add extensions to align with our contribution guidelines, and ensure that you have a consistent development environment.

If an AppHost endpoint is not reachable from the host machine, use VS Code's [port forwarding](https://code.visualstudio.com/docs/editor/port-forwarding) for the relevant DCP or resource port.

### 🛠️ Manual Setup

If you prefer not to use `devcontainer`, you can manually set up your development environment by installing the following tools:

-   [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) and [.NET 11](https://dotnet.microsoft.com/download/dotnet/11.0) SDKs
-   [Node.js LTS](https://nodejs.org/en/)
    -   [Yarn 2](https://yarnpkg.com/getting-started/install)
    -   [pnpm](https://pnpm.io/)
-   [Java JDK 21](https://learn.microsoft.com/java/openjdk/download)
    -   You'll also need [Gradle](https://gradle.org/install/) and [Apache Maven](https://maven.apache.org/download.cgi)
-   [Bun](https://bun.sh)
-   [Deno 2](https://deno.land/)
-   [Go 1.23](https://golang.org/)
-   [Python 3](https://www.python.org/downloads/)
    -   [Uvicorn](https://www.uvicorn.org/)
-   [Rust](https://www.rust-lang.org/tools/install)
-   [Docker](https://docs.docker.com/get-docker/)
    -   Podman is also supported, but Docker is recommended for consistency.

And of course, an editor such as Visual Studio, JetBrains Rider or emacs.

Toolkit packages target both `net10.0` and `net11.0`; tests and examples use the default `net10.0` target. CI installs both SDKs, builds and packages both target frameworks, and runs the full integration-test matrix on `net10.0`.

### Aspire nightly builds

The repository currently targets Aspire **17.0.0-preview.1.26555.11**, an unsupported nightly build from the public `dotnet9` feed. `nuget.config` maps `Aspire.*` packages to that feed; Elasticsearch remains on its last stable package because no Aspire 17 nightly is published for it.

`Directory.Build.props` pins hosting and testing packages, `global.json` pins the AppHost SDK, and example `aspire.config.json` files pin the polyglot SDK. Keep these versions aligned when upgrading. The nightly version already contains its prerelease suffix, so `AspirePreviewSuffix` is empty.

The C# AppHosts that host .NET projects now reference `Aspire.Hosting.Dotnet` and use `AddDotnetProject`; the TypeScript equivalents use `addDotnetProject`. `Aspire.Hosting.Dotnet` is centrally pinned to the same nightly version. The AppHosts still use `Aspire.AppHost.Sdk` for staged compatibility; Aspire 17 emits `ASPIRE012` for that obsolete SDK, and migrating to explicit `Aspire.Hosting.AppHost` references remains separate from this project-resource migration.

Install the matching CLI with `aspire.dev`'s installer and its explicit version option (`-Version 17.0.0-preview.1.26555.11` on Windows or `--version 17.0.0-preview.1.26555.11` on Linux/macOS), rather than selecting a moving daily channel. See the [upstream daily-build guide](https://github.com/microsoft/aspire/blob/main/docs/using-latest-daily.md) for installer instructions.
