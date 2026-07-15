# Logto Hosting Extensions for .NET Aspire

## Overview

This package provides **.NET Aspire hosting extensions** for integrating **Logto** with your AppHost.
It includes helpers for wiring Logto to **PostgreSQL** (via `Aspire.Hosting.Postgres.AddPostgres()`) and optional **Redis** caching, and exposes fluent APIs to configure the required environment variables for Logto database connectivity, initialization, and caching.
 
---

## Features

- Configure **Logto** to use **PostgreSQL** via `AddLogto(...)`.
- Optional **Redis** integration for caching via `.WithRedis(...)`.
- Fluent helpers to set environment variables:
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
    .WithTrustProxyHeader(true)      // optional override, default is already true
    .WithSensitiveUsername(true)
    .WithNodeEnv("production");
```

Logto will be configured with:

* `DB_URL=postgresql://.../logto_db` (constructed from the Postgres resource)
* `REDIS_URL=...` (when Redis is attached with `.WithRedis(...)`)
* `ADMIN_ENDPOINT=...` (when configured with `.WithAdminEndpoint(...)`)
* `NODE_ENV=production` (when configured with `.WithNodeEnv(...)`)
* Auto-configured health checks on `/api/status`.

---

## Notes

* Extension methods are in the `Aspire.Hosting` namespace.
* Call `.WithDatabaseSeeding()` to run the database seeding command
  `npm run cli db seed -- --swe && npm start` on startup.
* Container ports are **3001** (HTTP) and **3002** (Admin). Host ports are random by default unless explicitly configured.
* In an air-gapped environment, use `.WithDatabaseSeeding(disableAdminPwnedPasswordCheck: true)` to pass Logto's `--dapc` option during initial admin creation.
* `WithSensitiveUsername(...)` is deprecated in Logto 1.41. Configure username case sensitivity in the tenant settings in Logto Console.
* Container ports are **3001** (HTTP) and **3002** (Admin). Host ports are random by default unless explicitly configured; Logto receives the allocated public URLs automatically so browser redirects continue to work.
