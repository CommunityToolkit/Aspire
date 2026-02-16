# CommunityToolkit.Aspire.Hosting.Rust library

Provides extensions methods and resource definitions for the Aspire AppHost to support running Rust applications.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Rust
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Rust resource, then call `AddRustApp`:

```csharp
var rustApp = builder.AddRustApp("rust-app", "../actix_api");
```

### Alternative build tools

You can use alternative Rust build tools instead of the default `cargo run` by using the `WithCargoCommand` extension method:

```csharp
// Use trunk for WASM CSR applications
var wasmApp = builder.AddRustApp("wasm-app", "../wasm_frontend")
    .WithCargoCommand("trunk", "serve");

// Use cargo-leptos for Leptos SSR/hybrid applications
var leptosApp = builder.AddRustApp("leptos-app", "../leptos_app")
    .WithCargoCommand("cargo-leptos", "watch");
```

### Installing Rust tools

Use `WithCargoInstall` to automatically install a Rust tool before the application starts:

```csharp
// Install trunk and use it to serve a WASM app
var wasmApp = builder.AddRustApp("wasm-app", "../wasm_frontend")
    .WithCargoInstall("trunk")
    .WithCargoCommand("trunk", "serve");

// Install with a specific version and locked dependencies
var leptosApp = builder.AddRustApp("leptos-app", "../leptos_app")
    .WithCargoInstall("cargo-leptos", version: "0.2.0", locked: true)
    .WithCargoCommand("cargo-leptos", "watch");

// Use cargo-binstall for faster installs from pre-compiled binaries
var app = builder.AddRustApp("my-app", "../my_app")
    .WithCargoInstall("trunk", binstall: true)
    .WithCargoCommand("trunk", "serve");

// Enable specific features when installing
var app2 = builder.AddRustApp("my-app", "../my_app")
    .WithCargoInstall("cargo-leptos", features: ["ssr"]);
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-rust

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

