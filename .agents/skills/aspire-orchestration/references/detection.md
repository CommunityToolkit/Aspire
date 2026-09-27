# Detection — Recognizing Aspire Projects

> **Purpose**: How to identify that a project uses Aspire, and which project is the AppHost.

## Detection Signals

### 1. C# AppHost (Definitive — Strongest Signal)

Look for `.csproj` files containing the Aspire AppHost SDK reference, regardless of
its version:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Aspire.AppHost.Sdk" Version="13.5.3" />
  <PropertyGroup>
    <AspireUseCliBundle>true</AspireUseCliBundle>
  </PropertyGroup>
</Project>
```

**Detection method**: Search for `Aspire.AppHost.Sdk` in `.csproj` files:

```bash
grep -rl "Aspire.AppHost.Sdk" --include="*.csproj" .
```

This is the **definitive signal** — if a `.csproj` contains this SDK reference, it is an Aspire AppHost project. All Aspire CLI commands should target this project's directory.

### 1b. File-Based C# AppHost (Definitive)

Single-file C# AppHosts use `apphost.cs` (or similar `.cs` files) with SDK directives instead of a `.csproj`:

```cs
#:sdk Aspire.AppHost.Sdk
#:property IsAspireHost=true

var builder = DistributedApplication.CreateBuilder(args);
// ...
```

**Detection method**: Search for `apphost.cs` or `.cs` files containing `#:sdk Aspire.AppHost.Sdk`:

```bash
find . -name "apphost.cs" -not -path "*/node_modules/*"
grep -rl "#:sdk Aspire.AppHost.Sdk" --include="*.cs" .
```

File-based AppHosts are run the same way: `aspire start` (never `dotnet apphost.cs` directly).

### 2. TypeScript AppHost (Definitive)

Look for the current `apphost.mts` entry point first, then legacy `apphost.ts`:

```bash
find . \( -name "apphost.mts" -o -name "apphost.ts" \) -not -path "*/node_modules/*"
```

A current TypeScript AppHost imports `createBuilder` from
`./.aspire/modules/aspire.mjs`. Treat `apphost.ts` as a migration-compatible legacy
layout. Explain that `aspire update --migrate` updates Aspire packages and migration
artifacts together, then run `aspire update --migrate --yes --non-interactive` only after
the user approves both mutation scopes.

### 3. `.aspire/modules/` Directory (High Confidence)

Aspire generates a `.aspire/modules/` directory for TypeScript AppHost support files. Its presence strongly indicates an Aspire project:

```bash
[ -d ".aspire/modules" ] && echo "Aspire project detected"
```

### 4. `aspire.config.json` Configuration (High Confidence)

Aspire 13.2+ uses a rooted `aspire.config.json` file (replaces legacy `aspire.json`):

```bash
[ -f "aspire.config.json" ] && echo "Aspire configuration found"
# Legacy fallback:
[ -f "aspire.json" ] && echo "Legacy Aspire config found (pre-13.2)"
```

### 5. `.aspire/` Directory (High Confidence)

The `.aspire/` directory stores Aspire settings and secrets:

```bash
[ -d ".aspire" ] && echo "Aspire settings directory found"
```

### 6. Service Defaults References (Medium Confidence)

Projects that reference `Aspire.ServiceDefaults` are Aspire service projects (not the AppHost, but part of an Aspire solution):

```bash
grep -rl "Aspire.ServiceDefaults" --include="*.csproj" .
```

This indicates the project is **part of** an Aspire solution, but these are the service projects, not the AppHost. Look for the AppHost SDK reference separately.

---

## Detection Priority

When scanning a repository, check signals in this order:

| Priority | Signal | What It Means |
|----------|--------|---------------|
| 1 | `Aspire.AppHost.Sdk` in `.csproj` | This IS the AppHost — target for `aspire start` |
| 1b | `apphost.cs` or `#:sdk Aspire.AppHost.Sdk` in `.cs` | File-based C# AppHost — target for `aspire start` |
| 2 | `apphost.mts` or legacy `apphost.ts` file | TypeScript AppHost — target for `aspire start` |
| 3 | `.aspire/modules/` directory | Aspire project — look for the AppHost |
| 4 | `aspire.config.json` or `.aspire/` | Aspire project — look for the AppHost |
| 5 | `Aspire.ServiceDefaults` references | Part of Aspire solution — AppHost is elsewhere |

## Finding the AppHost Directory

The Aspire CLI commands must be run from the correct context. After detecting an Aspire project:

```bash
# Find the AppHost project directory
APPHOST_DIR=$(dirname $(grep -rl "Aspire.AppHost.Sdk" --include="*.csproj" .))

# Or for file-based C# AppHost
APPHOST_FILE=$(find . -name "apphost.cs" -not -path "*/node_modules/*" | head -1)

# Or for TypeScript
APPHOST_DIR=$(dirname $(find . \( -name "apphost.mts" -o -name "apphost.ts" \) -not -path "*/node_modules/*" | head -1))
```

## Common Project Structures

### Typical C# Aspire Solution

```
MyApp/
├── MyApp.AppHost/              ← AppHost (has Aspire.AppHost.Sdk)
│   ├── MyApp.AppHost.csproj
│   └── Program.cs
├── MyApp.ApiService/           ← Service project
│   └── MyApp.ApiService.csproj
├── MyApp.Web/                  ← Frontend project
│   └── MyApp.Web.csproj
├── MyApp.ServiceDefaults/      ← Shared defaults
│   └── MyApp.ServiceDefaults.csproj
├── .aspire/
│   └── modules/                ← Aspire-generated
├── aspire.config.json
└── MyApp.sln
```

### Typical TypeScript Aspire Project

```
MyApp/
├── apphost.mts                 ← TypeScript AppHost
├── package.json
├── src/
│   ├── api/                    ← Service project
│   └── web/                    ← Frontend project
├── .aspire/
│   └── modules/
└── aspire.config.json
```

## Non-Aspire Projects

If none of the detection signals are found, this is **not** an Aspire project. Do not apply Aspire-specific rules. Standard .NET commands (`dotnet run`, `dotnet build`) are appropriate for non-Aspire projects.
