# CommunityToolkit.Aspire.Hosting.Supabase provides Supabase for .NET Aspire

A complete Supabase stack integration for .NET Aspire, providing local development with full Supabase functionality including PostgreSQL, Auth (GoTrue), REST API (PostgREST), Storage, Kong API Gateway, Studio Dashboard, and Edge Functions.

## Table of Contents

- [Quick Start](#quick-start)
- [Configuration Options](#configuration-options)
- [Syncing from Remote Supabase Project](#syncing-from-remote-supabase-project)
- [Local Migrations](#local-migrations)
- [Edge Functions](#edge-functions)
- [Registered Users](#registered-users)
- [Sub-Resource Configuration](#sub-resource-configuration)
- [Dashboard Commands](#dashboard-commands)
- [Accessing Resources](#accessing-resources)
- [Environment Variables for Frontend](#environment-variables-for-frontend)

---

## Quick Start

The simplest way to add a complete Supabase stack to your Aspire application:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var supabase = builder.AddSupabase("supabase");

builder.Build().Run();
```

This starts a fully functional Supabase stack with:
- PostgreSQL database (port 54322)
- GoTrue authentication
- PostgREST API
- Storage API
- Kong API Gateway (port 8000)
- Studio Dashboard (port 54323)
- Postgres Meta

All services use sensible defaults and are ready for local development.

---

## Configuration Options

### Database Configuration

```csharp
var supabase = builder.AddSupabase("supabase")
    .ConfigureDatabase(db => db
        .WithPassword("my-secure-password")
        .WithPort(54322));
```

### All Available Configure Methods

Each sub-resource can be configured individually:

```csharp
var supabase = builder.AddSupabase("supabase")
    // PostgreSQL Database
    .ConfigureDatabase(db => db
        .WithPassword("secure-password")
        .WithPort(54322))

    // GoTrue Authentication
    .ConfigureAuth(auth => auth
        .WithAutoConfirm(true)
        .WithDisableSignup(false)
        .WithJwtExpiration(3600)
        .WithSiteUrl("http://localhost:3000"))

    // PostgREST API
    .ConfigureRest(rest => rest
        .WithSchemas("public", "storage", "graphql_public")
        .WithAnonRole("anon"))

    // Storage API
    .ConfigureStorage(storage => storage
        .WithFileSizeLimit(52428800))  // 50MB

    // Kong API Gateway
    .ConfigureKong(kong => kong
        .WithPort(8000))

    // Postgres Meta
    .ConfigureMeta(meta => meta
        .WithPort(8080))

    // Studio Dashboard
    .ConfigureStudio(studio => studio
        .WithPort(54323)
        .WithOrganizationName("My Organization")
        .WithProjectName("My Project"))

    // Edge Runtime
    .ConfigureEdgeRuntime(edge => edge
        .WithPort(9000));
```

### Direct Container Access

Each Configure method has an overload that provides direct access to the container builder:

```csharp
.ConfigureDatabase(
    db => db.WithPassword("password"),
    container => container
        .WithEnvironment("CUSTOM_VAR", "value")
        .WithVolume("my-volume", "/data"))
```

---

## Syncing from Remote Supabase Project

Synchronize schema, data, storage, and more from an existing Supabase cloud project:

### Basic Sync

```csharp
const string projectRef = "your-project-ref";
const string serviceKey = "eyJhbGciOiJIUzI1NiIs...";  // service_role key

var supabase = builder.AddSupabase("supabase")
    .WithProjectSync(projectRef, serviceKey);
```

### Sync Options

Control what gets synchronized using `SyncOptions`:

```csharp
var supabase = builder.AddSupabase("supabase")
    .WithProjectSync(
        projectRef,
        serviceKey,
        SyncOptions.Schema | SyncOptions.Data | SyncOptions.StorageBuckets);
```

Available options:

| Option | Description |
|--------|-------------|
| `Schema` | Table structures (columns, types, constraints) |
| `Data` | Table data |
| `Policies` | Row Level Security policies (requires DB password) |
| `Functions` | Stored procedures and functions (requires DB password) |
| `Triggers` | Database triggers (requires DB password) |
| `Types` | Custom types and enums (requires DB password) |
| `Views` | Database views (requires DB password) |
| `Indexes` | Table indexes (requires DB password) |
| `StorageBuckets` | Storage bucket definitions |
| `StorageFiles` | Storage files (downloads from remote) |
| `EdgeFunctions` | Edge Functions (requires Management API token) |
| `AllSchema` | All schema-related options |
| `AllStorage` | StorageBuckets + StorageFiles |
| `All` | Everything |

### Full Sync with Database Password

For complete schema sync including policies, functions, and triggers:

```csharp
const string dbPassword = "your-database-password";  // From Dashboard → Project Settings → Database

var supabase = builder.AddSupabase("supabase")
    .WithProjectSync(
        projectRef,
        serviceKey,
        SyncOptions.All,
        dbPassword);
```

### Edge Functions Sync

To sync Edge Functions, you need a Management API token:

```csharp
const string managementApiToken = "sbp_...";  // From Dashboard → Account → Access Tokens

var supabase = builder.AddSupabase("supabase")
    .WithProjectSync(
        projectRef,
        serviceKey,
        SyncOptions.All,
        dbPassword,
        managementApiToken);
```

> **Note:** The Supabase Management API returns compiled ESZIP bundles, not source code. The sync creates placeholder files with instructions to manually copy the source code from the Dashboard or use `supabase functions download`.

### Where to Find Keys

| Key | Location |
|-----|----------|
| Project Ref | Dashboard URL: `https://supabase.com/dashboard/project/{project-ref}` |
| Service Role Key | Dashboard → Project Settings → API → `service_role` (secret) |
| Database Password | Dashboard → Project Settings → Database → Database password |
| Management API Token | Dashboard → Account (top right) → Access Tokens |

---

## Local Migrations

Apply local SQL migration files to your Supabase instance:

```csharp
var migrationsPath = Path.Combine(builder.AppHostDirectory, "..", "supabase", "migrations");

var supabase = builder.AddSupabase("supabase")
    .WithMigrations(migrationsPath);
```

Migration files should follow the naming convention: `YYYYMMDDHHMMSS_description.sql`

Example structure:
```
supabase/
  migrations/
    20240101000000_create_users_table.sql
    20240102000000_add_profiles.sql
    20240103000000_create_policies.sql
```

---

## Edge Functions

### Using Local Edge Functions

Point to your local Edge Functions directory:

```csharp
var edgeFunctionsPath = Path.Combine(builder.AppHostDirectory, "..", "supabase", "functions");

var supabase = builder.AddSupabase("supabase")
    .WithEdgeFunctions(edgeFunctionsPath);
```

Expected directory structure:
```
supabase/
  functions/
    my-function/
      index.ts
    another-function/
      index.ts
```

### Edge Function Format

Each function should be in its own directory with an `index.ts` file:

```typescript
// supabase/functions/hello-world/index.ts
import { serve } from "https://deno.land/std@0.177.0/http/server.ts";

serve(async (req) => {
  const { name } = await req.json();

  return new Response(
    JSON.stringify({ message: `Hello ${name}!` }),
    { headers: { "Content-Type": "application/json" } }
  );
});
```

### Calling Edge Functions

Edge Functions are available through Kong at:
```
http://localhost:8000/functions/v1/{function-name}
```

Example:
```bash
curl -X POST http://localhost:8000/functions/v1/hello-world \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ANON_KEY" \
  -d '{"name": "World"}'
```

---

## Registered Users

Pre-create users for development and testing:

```csharp
var supabase = builder.AddSupabase("supabase")
    .WithRegisteredUser("admin@example.com", "password123", "Admin User")
    .WithRegisteredUser("test@example.com", "test1234", "Test User");
```

These users:
- Are created with confirmed email status
- Automatically get a profile in `public.profiles` (if table exists)
- Automatically get an admin role in `public.user_roles` (if table exists)

---

## Dashboard Commands

Add a "Clear All Data" button to the Aspire dashboard:

```csharp
var supabase = builder.AddSupabase("supabase")
    .WithClearCommand();
```

This adds a command in the Aspire dashboard that truncates all tables in the `public` schema.

---

## Accessing Resources

### Get Sub-Resource Builders

Access individual container resources for advanced configuration:

```csharp
var supabase = builder.AddSupabase("supabase");

var kong = supabase.GetKong();
var studio = supabase.GetStudio();
var database = supabase.GetDatabase();
var auth = supabase.GetAuth();
var rest = supabase.GetRest();
var storage = supabase.GetStorage();
var meta = supabase.GetMeta();
var edge = supabase.GetEdgeRuntime();
```

### Access Keys and Endpoints

```csharp
var supabase = builder.AddSupabase("supabase");

// Get the anon key for client-side use
var anonKey = supabase.Resource.AnonKey;

// Get the service role key for server-side use
var serviceRoleKey = supabase.Resource.ServiceRoleKey;

// Get the Kong endpoint (main API gateway)
var kongEndpoint = supabase.Resource.Kong!.GetEndpoint("http");
```

---

## Environment Variables for Frontend

Configure your frontend application with Supabase environment variables:

```csharp
var supabase = builder.AddSupabase("supabase");

var frontend = builder.AddNpmApp("frontend", "../frontend")
    .WithEnvironment("VITE_SUPABASE_URL", supabase.Resource.Kong!.GetEndpoint("http"))
    .WithEnvironment("VITE_SUPABASE_ANON_KEY", supabase.Resource.AnonKey);
```

Or for a JavaScript/TypeScript app:

```csharp
var frontend = builder.AddJavaScriptApp("frontend", "../frontend", "dev")
    .WithEnvironment("VITE_SUPABASE_URL", supabase.Resource.Kong!.GetEndpoint("http"))
    .WithEnvironment("VITE_SUPABASE_PUBLISHABLE_KEY", supabase.Resource.AnonKey);
```

---

## Complete Example

Here's a complete example combining multiple features:

```csharp
using MandateManager.AppHost.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

// Paths
var supabasePath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "supabase"));
var migrationsPath = Path.Combine(supabasePath, "migrations");
var edgeFunctionsPath = Path.Combine(supabasePath, "functions");

// Supabase configuration
var supabase = builder.AddSupabase("supabase")
    // Database settings
    .ConfigureDatabase(db => db
        .WithPassword("secure-dev-password")
        .WithPort(54322))

    // Studio settings
    .ConfigureStudio(studio => studio
        .WithPort(54323)
        .WithProjectName("My Project"))

    // Apply local migrations
    .WithMigrations(migrationsPath)

    // Enable local Edge Functions
    .WithEdgeFunctions(edgeFunctionsPath)

    // Pre-create dev users
    .WithRegisteredUser("admin@example.com", "Admin123!", "Admin User")
    .WithRegisteredUser("user@example.com", "User123!", "Regular User")

    // Add clear data command to dashboard
    .WithClearCommand();

// Frontend
var frontend = builder.AddJavaScriptApp("frontend", "../..", "dev")
    .WithHttpEndpoint(port: 3000, targetPort: 3000)
    .WithEnvironment("VITE_SUPABASE_URL", supabase.Resource.Kong!.GetEndpoint("http"))
    .WithEnvironment("VITE_SUPABASE_ANON_KEY", supabase.Resource.AnonKey);

builder.Build().Run();
```

---

## Default Ports

| Service | Default Port |
|---------|--------------|
| PostgreSQL | 54322 |
| Kong (API Gateway) | 8000 |
| Studio Dashboard | 54323 |
| GoTrue (Auth) | 9999 (internal) |
| PostgREST | 3000 (internal) |
| Storage API | 5000 (internal) |
| Postgres Meta | 8080 (internal) |
| Edge Runtime | 9000 (internal) |

---

## Troubleshooting

### Edge Functions not working

1. Check that the functions directory exists and contains subdirectories with `index.ts` files
2. Verify the function follows the expected format with `serve()` from Deno std library
3. Check the Edge Runtime container logs in the Aspire dashboard

### Database connection issues

1. Ensure no other PostgreSQL instance is running on port 54322
2. Check the database container logs for startup errors
3. Verify the password matches between configurations

### Sync not working

1. Verify the service key is the `service_role` key (starts with `eyJ...`), not the CLI key
2. For full schema sync, ensure the database password is provided
3. Check the console output for specific sync errors

