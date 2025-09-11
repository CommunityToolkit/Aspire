# Keycloak PostgreSQL Aspire Extension

## Overview

This package provides an **Aspire extension** for integrating **Keycloak** with **PostgreSQL**.
It works with resources created via `Aspire.Hosting.Postgres.AddPostgres()` and `Aspire.Hosting.Keycloak.AddKeycloak()`, and automatically configures the required environment variables for Keycloak database connectivity.

---

## Features

* Configures **Keycloak** to use PostgreSQL.
* Automatically sets environment variables:

    * `KC_DB`
    * `KC_DB_URL`
    * `KC_DB_USERNAME`
    * `KC_DB_PASSWORD`
* Supports **XA transactions** via `KC_TRANSACTION_XA_ENABLED`.
* Integrates with **ParameterResource** for secure user/password injection.
* Falls back to **default credentials** (`postgres` / `postgres`) if no parameters are provided.
* Fluent **extension methods** for easy configuration.

---

## Usage

### Installation

Add the extension to your Aspire client project.
Use the `WithPostgres(...)` extension method when configuring your Keycloak resource.

---

### Example Usage

#### With explicit username and password parameters

```csharp
var postgres = builder.AddPostgres("pg");
var db = postgres.AddDatabase("keycloakdb");
var user = builder.AddParameter("keycloak-user");
var pass = builder.AddParameter("keycloak-pass");

var keycloak = builder.AddKeycloak("kc")
    .WithPostgres(db, user, pass);
```

Keycloak will be configured with:

* `KC_DB=postgres`
* `KC_DB_URL=jdbc:postgresql://<host>:<port>/keycloakdb`
* `KC_DB_USERNAME=<value of user>`
* `KC_DB_PASSWORD=<value of pass>`

---

#### With default Postgres parameters

```csharp
var postgres = builder.AddPostgres("pg");
var db = postgres.AddDatabase("keycloakdb");

var keycloak = builder.AddKeycloak("kc")
    .WithPostgres(db);
```

If the Postgres server has `UserNameParameter` and `PasswordParameter`, they will be used automatically.
If not, the default values `postgres` / `postgres` are applied.

---

#### Enabling XA transactions

```csharp
var keycloak = builder.AddKeycloak("kc")
    .WithPostgres(db, xaEnabled: true);
```

In this case, Keycloak will also receive:

* `KC_TRANSACTION_XA_ENABLED=true`

---

## API Reference

 `WithPostgres(this IResourceBuilder<KeycloakResource> builder, IResourceBuilder<PostgresDatabaseResource> database, IResourceBuilder<ParameterResource> username, IResourceBuilder<ParameterResource> password, bool xaEnabled = false)`

Configures Keycloak with PostgreSQL using explicit username and password parameters.

---

`WithPostgres(this IResourceBuilder<KeycloakResource> builder, IResourceBuilder<PostgresDatabaseResource> database, bool xaEnabled = false)`

Configures Keycloak with PostgreSQL using default credentials (taken from the `PostgresServerResource` if available, otherwise defaults to `postgres` / `postgres`).