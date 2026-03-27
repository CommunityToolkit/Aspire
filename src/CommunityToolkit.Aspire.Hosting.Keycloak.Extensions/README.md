# Keycloak Hosting Extensions for Aspire

## Overview

This package provides **Aspire hosting extensions** for integrating **Keycloak** with your AppHost.
It includes a PostgreSQL integration that works with resources created via `Aspire.Hosting.Postgres.AddPostgres()` and `Aspire.Hosting.Keycloak.AddKeycloak()`, and automatically configures the required environment variables for Keycloak database connectivity.

---

## Features

* Configure **Keycloak** to use **PostgreSQL**.
* Automatically sets environment variables:
    * `KC_DB`
    * `KC_DB_URL`
    * `KC_DB_USERNAME`
    * `KC_DB_PASSWORD`
* Supports **XA transactions** via `KC_TRANSACTION_XA_ENABLED`.
* Integrates with **ParameterResource** for secure user/password injection.
* Falls back to **default credentials** (`postgres` / `postgres`) if no parameters are provided.
* Fluent **extension methods** on the hosting model.

---

## Usage (AppHost)

```csharp
var postgres = builder.AddPostgres("pg");
var db = postgres.AddDatabase("keycloakdb");

// Using explicit username/password parameters
var user = builder.AddParameter("keycloak-user");
var pass = builder.AddParameter("keycloak-pass");

var keycloak = builder.AddKeycloak("kc")
    .WithPostgres(db, user, pass);

// Or rely on server parameters or default postgres/postgres
var keycloak2 = builder.AddKeycloak("kc2")
    .WithPostgres(db, xaEnabled: true);
```

Keycloak will be configured with:

* `KC_DB=postgres`
* `KC_DB_URL=jdbc:postgresql://<host>:<port>/keycloakdb`
* `KC_DB_USERNAME=<user>`
* `KC_DB_PASSWORD=<pass>`
* `KC_TRANSACTION_XA_ENABLED=true` (when set)

---

## Notes

* Extension methods are in the `Aspire.Hosting` namespace for discoverability in AppHost projects.
* If you add custom resource types, place them under `Aspire.Hosting.ApplicationModel`.