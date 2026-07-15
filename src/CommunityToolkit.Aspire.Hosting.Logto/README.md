# Logto Hosting Extensions for .NET Aspire

## Overview

This package provides **.NET Aspire hosting extensions** for integrating **Logto** with your AppHost.
It includes helpers for wiring Logto to **PostgreSQL** (via `Aspire.Hosting.Postgres.AddPostgres()`) and optional **Redis** caching, and exposes fluent APIs to configure the required environment variables for Logto database connectivity, initialization, and caching.
 
---

## Features

- Configure **Logto** to use **PostgreSQL** via `AddLogto(...)`.
- Optional **Redis** integration for caching via `.WithRedis(...)`.
- Fluent helpers to set environment variables:
  - `ENDPOINT` (automatically set from the allocated primary endpoint during local runs)
  - `DB_URL` (Postgres connection string)
  - `REDIS_URL`
  - `NODE_ENV`
  - `ADMIN_ENDPOINT`
- Data persistence via:
  - `.WithDataVolume()` (managed Docker volume)
  - `.WithDataBindMount()` (host bind mount).
- Configurable **Admin Console** access and **proxy header** trust (`TRUST_PROXY_HEADER`).
- Built-in health check for `/api/status`.

---

## Usage (AppHost)

```csharp
using Aspire.Hosting;
using Aspire.Hosting.Postgres;
var postgres = builder.AddPostgres("postgres");

// Basic setup connecting to Postgres
var logto = builder
    .AddLogto("logto", postgres)
    .WithDatabaseSeeding();

// Advanced setup with Redis and specific configurations
var redis = builder.AddRedis("redis");

var logtoSecure = builder
    .AddLogto("logto-secure", postgres, databaseName: "logto_secure_db")
    .WithRedis(redis)
    .WithAdminEndpoint("https://admin.example.com")
    .WithDisableAdminConsole(false)
    .WithTrustProxyHeader(true);     // needed when TLS is terminated by a reverse proxy
```

Logto will be configured with:

* `DB_URL=postgresql://.../logto_db` (constructed from the Postgres resource)
* `REDIS_URL=...` (when Redis is attached with `.WithRedis(...)`)
* `ENDPOINT=...` (automatically set to the allocated public HTTP URL during local runs)
* `ADMIN_ENDPOINT=...` (automatically set to the allocated Admin Console URL during local runs, or overridden with `.WithAdminEndpoint(...)`)
* `NODE_ENV` (when explicitly configured with `.WithNodeEnv(...)`)
* Auto-configured health checks on `/api/status`.

---

## Local Admin Console and CORS

Logto Admin Console and the Logto API use separate ports. During a local Aspire run, both ports are exposed through dynamically allocated loopback proxy URLs. For example:

```text
Admin Console: http://127.0.0.1:10612
Logto API:     http://localhost:10611
```

Logto's production CORS policy intentionally rejects localhost origins when both its internal Admin URL and an external Admin endpoint are present. The symptom is that pages in Admin Console fail to load data or create resources such as Applications, and the browser reports an error similar to:

```text
Access to fetch at 'http://localhost:10611/api/resources' from origin
'http://localhost:10612' has been blocked by CORS policy:
No 'Access-Control-Allow-Origin' header is present.
```

This integration handles the local Aspire scenario automatically:

- `ENDPOINT` is populated from the allocated Aspire API proxy URL.
- The local `ADMIN_ENDPOINT` and the Admin link shown in Aspire Dashboard use `127.0.0.1` with the allocated Admin proxy port.
- Logto keeps its normal production startup, so the compiled Admin Console is served instead of attempting to contact the unavailable Vite development server.

> [!IMPORTANT]
> Open the exact `admin` URL shown in Aspire Dashboard. Do not replace `127.0.0.1` with `localhost`: Logto 1.41 intentionally removes `localhost` from its production CORS allowlist when an external Admin endpoint is configured.

Do not use the direct Docker container port, because it may not match `ADMIN_ENDPOINT`. After changing endpoints, restart the Logto resource and reload the browser without cache.

For HTTPS termination at a reverse proxy, additionally configure `.WithTrustProxyHeader(true)` and ensure the proxy sends `X-Forwarded-Proto`.

---

## Notes

* Extension methods are in the `Aspire.Hosting` namespace.
* Call `.WithDatabaseSeeding()` to run database seeding and the required Logto schema alterations in a one-shot setup container before Logto starts. This is required when upgrading an existing database to Logto 1.40 or later.
* Local Admin links use `127.0.0.1` rather than `localhost` to remain compatible with Logto 1.41 production CORS checks while still serving the compiled Admin Console.
* In an air-gapped environment, use `.WithDatabaseSeeding(disableAdminPwnedPasswordCheck: true)` to pass Logto's `--dapc` option during initial admin creation.
* `WithSensitiveUsername(...)` is deprecated in Logto 1.41. Configure username case sensitivity in the tenant settings in Logto Console.
* Container ports are **3001** (HTTP) and **3002** (Admin). Host ports are random by default unless explicitly configured; Logto receives the allocated public URLs automatically so browser redirects continue to work.
