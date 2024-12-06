# 🛠️ Setting up your Development Environment

Because the Community Toolkit consists of integrations across a lot of different runtimes, setting up a development environment can be a bit complex. Below we outline the recommended approach to developing, as well as a manual setup if you prefer not to use the recommended approach.

## ✅ Recommended Setup

The easiest, and recommended way it to use [VS Code](https://code.visualstudio.com/) with the [`devcontainer`](https://code.visualstudio.com/docs/remote/containers) extension.

This will run the development environment in a container, install all the necessary tools and dependencies, add extensions to align with our contribution guidelines, and ensure that you have a consistent development environment.

> Note: There is an issue with devcontainers in that the ports bound by the DCP (the thing the app host uses to orchestrate behind the scenes) are not exposed to the host machine, meaning that the HTTP endpoints fail to resolve. This can be fixed by manually [forwarding the port](https://code.visualstudio.com/docs/editor/port-forwarding). This is a known issue in Aspire and being tracked for a 9.1 fix 🤞.

### 🛠️ Manual Setup

If you prefer not to use `devcontainer`, you can manually set up your development environment by installing the following tools:

-   [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) and [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0)
-   [Node.js LTS](https://nodejs.org/en/)
-   [Java JDK 11](https://learn.microsoft.com/java/openjdk/download)
-   [Bun](https://bun.sh)
-   [Deno 2](https://deno.land/)
-   [Go 1.23](https://golang.org/)
-   [Python 3](https://www.python.org/downloads/)
-   [Rust](https://www.rust-lang.org/tools/install)
-   [Docker](https://docs.docker.com/get-docker/)
    -   Podman is also supported, but Docker is recommended for consistency.

And of course, an editor such as Visual Studio, JetBrains Rider or emacs.
