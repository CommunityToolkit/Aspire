# 🛠️ Setting up your Development Environment

Because the Community Toolkit consists of integrations across a lot of different runtimes, setting up a development environment can be a bit complex. Below we outline the recommended approach to developing, as well as a manual setup if you prefer not to use the recommended approach.

## ✅ Recommended Setup

The easiest, and recommended way is to use [VS Code](https://code.visualstudio.com/) with the [`devcontainer`](https://code.visualstudio.com/docs/remote/containers) extension.

This will run the development environment in a container, install all the necessary tools and dependencies, add extensions to align with our contribution guidelines, and ensure that you have a consistent development environment.

If an AppHost endpoint is not reachable from the host machine, use VS Code's [port forwarding](https://code.visualstudio.com/docs/editor/port-forwarding) for the relevant DCP or resource port.

### 🛠️ Manual Setup

If you prefer not to use `devcontainer`, you can manually set up your development environment by installing the following tools:

-   [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0), [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0), and [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)
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
