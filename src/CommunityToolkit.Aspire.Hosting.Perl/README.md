# CommunityToolkit.Aspire.Hosting.Perl library

Provides extensions methods and resource definitions for the .NET Aspire AppHost to support running Perl.

## Using the Perl Hosting Integration

A guide for developers using `CommunityToolkit.Aspire.Hosting.Perl` for the first time, or as a
reference when revisiting the API. This document explains how key hosting API calls map to on-disk
directory layout, environment variable configuration, and runtime behavior.

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [Core Concepts](#core-concepts)
3. [The `appDirectory` Parameter](#the-appdirectory-parameter)
4. [WithLocalLib](#withlocallib)
5. [Package Management](#package-management)
   - [WithCpanMinus + WithPackage](#withcpanminus--withpackage)
   - [WithCpanMinus + WithProjectDependencies](#withcpanminus--withprojectdependencies)
   - [WithCarton + WithProjectDependencies](#withcarton--withprojectdependencies)
6. [WithPerlbrewEnvironment](#withperlbrewenvironment)
7. [Example Layouts](#example-layouts)
8. [Common Pitfalls](#common-pitfalls)

---

## Quick Start

Install the package in your AppHost project:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Perl
```

Add a Perl script resource in your `AppHost.cs`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddPerlScript("my-worker", "scripts", "Worker.pl")
    .WithCpanMinus()
    .WithPackage("Some::Module", skipTest: true)
    .WithLocalLib("local");

builder.Build().Run();
```

---

## Core Concepts

The integration provides two entry points for adding Perl resources:

| Method | Purpose |
|--------|---------|
| `AddPerlScript(name, appDirectory, scriptName)` | Adds a Perl script (worker, CLI tool, etc.) |
| `AddPerlApi(name, appDirectory, scriptName)` | Adds a Perl API server (e.g., Mojolicious `daemon`) |

Both create a `PerlAppResource` that appears in the Aspire dashboard. All subsequent configuration
methods (`.WithCpanMinus()`, `.WithLocalLib()`, etc.) chain off the resource builder.

---

## The `appDirectory` Parameter

`appDirectory` is the **anchor for all relative path resolution** in the integration. It determines:

- The resource's `WorkingDirectory` — where Perl runs
- Where `WithLocalLib("local")` resolves to
- Where cpanfile discovery happens (for `WithProjectDependencies`)
- The base for the script path

`appDirectory` is resolved relative to the **AppHost project directory** (the folder containing
the `.csproj`).

### `"."` — AppHost-rooted

When `appDirectory` is `"."`, the working directory is the AppHost project folder itself. Files like
`cpanfile`, `cpanfile.snapshot`, and the `local/` directory all live alongside the `.csproj`:

```
MyApp.AppHost/
├── AppHost.cs
├── MyApp.AppHost.csproj
├── cpanfile              ← discovered here
├── cpanfile.snapshot
├── local/                ← WithLocalLib("local") resolves here
│   └── lib/perl5/...
└── Properties/
scripts/
└── API.pl                ← script path "../scripts/API.pl"
```

### `"../scripts"` — sibling folder

When `appDirectory` is `"../scripts"`, the working directory shifts to a sibling `scripts/` folder.
Everything resolves relative to that folder:

```
MyApp.AppHost/
├── AppHost.cs
├── MyApp.AppHost.csproj
└── Properties/
scripts/                   ← working directory
├── Worker.pl              ← script path "Worker.pl"
└── local/                 ← WithLocalLib("local") resolves here
    └── lib/perl5/...
```

> **Key insight:** The script path in `AddPerlScript` / `AddPerlApi` is relative to `appDirectory`,
> and so is everything else — `WithLocalLib`, cpanfile discovery, and the process working directory.

---

## WithLocalLib

```csharp
.WithLocalLib("local")         // relative path — resolved against appDirectory
.WithLocalLib("/opt/lib")      // rooted Unix-style path — used as-is
.WithLocalLib("C:\\perl-lib") // rooted Windows path — used as-is
```

`WithLocalLib` configures [local::lib](https://metacpan.org/pod/local::lib)-style module isolation.
The `path` parameter is resolved **relative to the resource's working directory** (`appDirectory`),
not relative to the AppHost project, unless the path is already rooted.

Implementation note: `WithLocalLib` path resolution uses `Path.IsPathRooted(configuredPath)`.
If `true`, the value is used directly. If `false`, it is combined with the resource working
directory and converted to an absolute path.

### What it sets

| Environment Variable | Value |
|---------------------|-------|
| `PERL5LIB` | `<resolved>/lib/perl5` |
| `PERL_LOCAL_LIB_ROOT` | `<resolved>` |
| `PERL_MM_OPT` | `INSTALL_BASE=<resolved>` |
| `PERL_MB_OPT` | `--install_base <resolved>` |

These ensure that:
- Perl finds modules in the local directory at runtime (`@INC`)
- Package managers install modules into the local directory
- No `sudo` or system-level permissions required

### Resolution examples

| `appDirectory` | `WithLocalLib(...)` | Resolved absolute path |
|----------------|---------------------|----------------------|
| `"."` | `"local"` | `<AppHost>/local` |
| `"../scripts"` | `"local"` | `<AppHost>/../scripts/local` |
| `"."` | `"/opt/perl-libs"` | `/opt/perl-libs` (Linux/macOS) |
| `"."` | `"C:\\perl-libs"` | `C:\\perl-libs` (Windows) |

---

## Package Management

The integration supports three package managers and two installation strategies:

| Package Manager | Individual Packages | Project Dependencies |
|----------------|-------------------|---------------------|
| **cpan** (default) | ✅ `.WithPackage("Module")` | ❌ Not supported (auto-switches to cpanm when calling `.WithProjectDependencies()`) |
| **cpanm** (App::cpanminus) | ✅ `.WithCpanMinus().WithPackage("Module")` | ✅ `.WithCpanMinus().WithProjectDependencies()` |
| **Carton** | ❌ Not supported | ✅ `.WithCarton().WithProjectDependencies()` |

> The default package manager is `cpan`, but it is automatically switched to `cpanm` when
> `WithProjectDependencies()` is called, since `cpan` does not support `--installdeps`.

### WithCpanMinus + WithPackage

Installs individual modules by name before the application starts.

```csharp
builder.AddPerlScript("worker", "../scripts", "Worker.pl")
    .WithCpanMinus()
    .WithPackage("OpenTelemetry::SDK", skipTest: true)
    .WithLocalLib("local");
```

**What happens at startup:**
1. A child installer resource runs `cpanm --notest --local-lib <resolved>/local OpenTelemetry::SDK`
2. The module is installed into `scripts/local/lib/perl5/`
3. After installation, the main script starts with `PERL5LIB` pointing to the local directory

**Resulting directory structure:**

```
my-example/
├── MyExample.AppHost/
│   ├── AppHost.cs
│   └── MyExample.AppHost.csproj
└── scripts/                       ← working directory (appDirectory = "../scripts")
    ├── Worker.pl
    └── local/
        └── lib/
            └── perl5/
                └── OpenTelemetry/
                    └── SDK.pm
```

**Options:**

| Parameter | Effect |
|-----------|--------|
| `force: true` | Passes `--force` — reinstalls even if already present |
| `skipTest: true` | Passes `--notest` — skips running the module's test suite |

### WithCpanMinus + WithProjectDependencies

Installs all modules listed in a `cpanfile` in the working directory.

```csharp
builder.AddPerlApi("api", ".", "../scripts/API.pl")
    .WithCpanMinus()
    .WithProjectDependencies()
    .WithLocalLib("local");
```

**What happens at startup:**
1. The integration looks for `cpanfile` in the working directory
2. Runs `cpanm --installdeps --notest .` (with `--local-lib` if configured)
3. All dependencies from the cpanfile are installed

**Expected cpanfile location:** `<appDirectory>/cpanfile`

### WithCarton + WithProjectDependencies

[Carton](https://metacpan.org/pod/Carton) is a dependency manager for Perl that provides
reproducible builds via a lock file (`cpanfile.snapshot`).

```csharp
builder.AddPerlApi("api", ".", "../scripts/API.pl")
    .WithCarton()
    .WithProjectDependencies(cartonDeployment: false)
    .WithLocalLib("local");
```

**What happens at startup:**
1. The integration looks for `cpanfile` and optionally `cpanfile.snapshot` in the working directory
2. Runs `carton install` (or `carton install --deployment` if `cartonDeployment: true`)
3. Carton creates `local/` adjacent to the `cpanfile`

**Deployment mode (`cartonDeployment: true`):** Installs exact versions from `cpanfile.snapshot`,
ensuring production builds match development. Fails if the snapshot is missing or out of date.

**Resulting directory structure (appDirectory = "."):**

```
my-example/
├── MyApp.AppHost/                ← working directory (appDirectory = ".")
│   ├── AppHost.cs
│   ├── MyApp.AppHost.csproj
│   ├── cpanfile
│   ├── cpanfile.snapshot
│   └── local/
│       └── lib/
│           └── perl5/
│               ├── Mojolicious/
│               └── ...
└── scripts/
    └── API.pl                    ← script path "../scripts/API.pl"
```

> **Important:** Carton only supports project-level dependency installation. Calling `.WithPackage()`
> after `.WithCarton()` will throw an `InvalidOperationException`. If you need to install individual
> modules alongside Carton-managed dependencies, use `.WithCpanMinus()` on a separate resource.

---

## WithPerlbrewEnvironment

[Perlbrew](https://perlbrew.pl/) manages multiple Perl installations. This method configures the
resource to use a specific perlbrew-managed Perl version.

```csharp
builder.AddPerlScript("perlbrew-worker", "../scripts", "Worker.pl")
    .WithPerlbrewEnvironment("perl-5.42.0");
```

**What it configures:**
- Resolves the Perl binary from the perlbrew installation
- Sets `PERLBREW_ROOT`, `PERLBREW_PERL`, and `PERLBREW_HOME`
- Prepends the perlbrew `bin/` to `PATH`

**Interaction with WithLocalLib:** If `.WithLocalLib("local")` is chained, modules are installed
into the local directory, not the perlbrew tree. This keeps the perlbrew installation clean and
allows per-project isolation. `WithLocalLib` is optional when using perlbrew.

> **Note:** Perlbrew is Linux-only. On Windows, the integration will display a notification
> recommending [Berrybrew](https://github.com/stevieb9/berrybrew). Windows support for Berrybrew
> is on the roadmap.

---

## Example Layouts

Below you can find a directory structure with hints for how to visualize the project structure from the apphost entries.

### cpan-script-minimal

**Pattern:** cpanm + WithPackage + WithLocalLib, `appDirectory = "../scripts"`

```csharp
builder.AddPerlScript("cpan-worker", "../scripts", "Worker.pl")
    .WithPackage("OpenTelemetry::SDK", skipTest: true)
    .WithLocalLib("local");
```

```
cpan-script-minimal/
├── CpanScriptMinimal.AppHost/
│   ├── AppHost.cs
│   └── CpanScriptMinimal.AppHost.csproj
└── scripts/                        ← working directory
    ├── Worker.pl
    └── local/                      ← WithLocalLib("local") resolves here
        └── lib/perl5/
            └── OpenTelemetry/...
```

### cpanm-api-integration

**Pattern:** cpanm + WithPackage + WithLocalLib + HTTP endpoint, `appDirectory = "."`

```csharp
builder.AddPerlApi("perl-api", ".", "../scripts/API.pl")
    .WithCpanMinus()
    .WithPackage("Mojolicious::Lite", force: true, skipTest: true)
    .WithLocalLib("local")
    .WithHttpEndpoint(name: "http", env: "PORT");
```

```
cpanm-api-integration/
├── CpanmApiIntegration.AppHost/    ← working directory (appDirectory = ".")
│   ├── AppHost.cs
│   ├── CpanmApiIntegration.AppHost.csproj
│   └── local/                      ← WithLocalLib("local") resolves here
│       └── lib/perl5/
│           └── Mojolicious/...
└── scripts/
    └── API.pl                      ← script path "../scripts/API.pl"
```

### carton-api-minimal

**Pattern:** Carton + WithProjectDependencies + WithLocalLib, `appDirectory = "."`

```csharp
builder.AddPerlApi("carton-api", ".", "../scripts/API.pl")
    .WithCarton()
    .WithProjectDependencies(cartonDeployment: false)
    .WithLocalLib("local");
```

```
carton-api-minimal/
├── CartonApiMinimal.AppHost/       ← working directory (appDirectory = ".")
│   ├── AppHost.cs
│   ├── CartonApiMinimal.AppHost.csproj
│   ├── cpanfile                    ← Carton reads dependencies from here
│   ├── cpanfile.snapshot           ← lock file for reproducible installs
│   └── local/                      ← carton install creates this
│       └── lib/perl5/...
└── scripts/
    └── API.pl
```

### multi-resource

**Pattern:** Multiple Perl resources with different package managers, plus a .NET frontend

```csharp
// Carton-managed API (appDirectory = ".")
builder.AddPerlApi("perl-api", ".", "../scripts/API.pl")
    .WithCarton()
    .WithProjectDependencies(cartonDeployment: false)
    .WithLocalLib("local");

// cpanm-managed worker (appDirectory = "../scripts")
builder.AddPerlScript("perl-worker", "../scripts", "Worker.pl")
    .WithCpanMinus()
    .WithPackage("OpenTelemetry::SDK", force: true, skipTest: true)
    .WithLocalLib("local");
```

```
multi-resource/
├── MultiResource.AppHost/         ← working directory for the APIs
│   ├── AppHost.cs
│   ├── cpanfile                   ← shared by Carton-managed resources
│   ├── cpanfile.snapshot
│   └── local/                     ← Carton's "local" for the APIs
│       └── lib/perl5/...
├── scripts/                       ← working directory for the worker
│   ├── API.pl
│   ├── secondLayerApi.pl
│   ├── Worker.pl
│   └── local/                     ← cpanm's "local" for the worker
│       └── lib/perl5/
│           └── OpenTelemetry/...
└── MultiResource.Driver/          ← .NET Blazor frontend
```

> Note how the same `WithLocalLib("local")` call produces *different* absolute paths depending
> on `appDirectory`:
> - API resources (`appDirectory = "."`) → `MultiResource.AppHost/local/`
> - Worker (`appDirectory = "../scripts"`) → `scripts/local/`

> **Linux HTTPS note:** The multi-resource example uses HTTPS for service-to-service calls between
> Perl API resources. On Windows, `dotnet dev-certs https --trust` adds the Aspire dev certificate
> to the OS certificate store automatically. On Linux, OpenSSL does **not** trust the dev cert by
> default, so `Mojo::UserAgent` HTTPS requests between resources will fail with
> `certificate verify failed`.
>
> To fix this, export the dev cert and add it to your system CA store:
>
> ```bash
> # Export the dev cert as PEM
> dotnet dev-certs https --export-path /tmp/aspire-dev-cert.crt --format PEM --no-password
>
> # Ubuntu/Debian
> sudo cp /tmp/aspire-dev-cert.crt /usr/local/share/ca-certificates/aspire-dev.crt
> sudo update-ca-certificates
>
> # Fedora/RHEL
> sudo cp /tmp/aspire-dev-cert.crt /etc/pki/ca-trust/source/anchors/aspire-dev.crt
> sudo update-ca-trust
> ```
>
> This is a one-time setup per machine. After this, HTTPS between Perl resources works identically
> to Windows.

---

## Common Pitfalls

### WithLocalLib resolves relative to `appDirectory`, not the AppHost

```csharp
// appDirectory = "../scripts", WithLocalLib("local")
// ✅ Resolves to: scripts/local/
// ❌ Does NOT resolve to: MyApp.AppHost/local/
```

If you expect the `local/` folder next to your `.csproj`, set `appDirectory` to `"."`.

### Choosing to skip WithLocalLib modifies shared Perl installs

It is valid to skip `WithLocalLib` if you intentionally want a shared/global module install.
That can be useful for common libraries on dev machines.

The tradeoff is that installs target your platform Perl distribution instead of a project-local
folder. In practice this often means:

- Linux (especially OS-managed Perl): writes to system or user Perl paths and may require elevated permissions
- Windows: writes into the active Strawberry Perl or ActiveState Perl environment

This can be convenient, but it can also create drift across machines and affect unrelated projects.
Proceed with caution.

### Mixing WithCarton and WithPackage

Carton manages all dependencies through `cpanfile`. Calling `.WithPackage()` after `.WithCarton()`
will throw an `InvalidOperationException`:

```csharp
// ❌ This throws — Carton does not support individual module installation
builder.AddPerlApi("api", ".", "api.pl")
    .WithCarton()
    .WithPackage("Some::Module");

// ✅ Instead, add the module to your cpanfile:
//    requires 'Some::Module';
```

### Script path is relative to `appDirectory`

The `scriptName` parameter is resolved relative to `appDirectory`. Don't include the `appDirectory`
in the script path:

```csharp
// ❌ Double-nests the path
builder.AddPerlScript("worker", "../scripts", "../scripts/Worker.pl");

// ✅ Correct — script path is relative to appDirectory
builder.AddPerlScript("worker", "../scripts", "Worker.pl");
```

### cpanfile must be in the working directory

`WithProjectDependencies` looks for `cpanfile` in the resource's working directory (`appDirectory`).
If your cpanfile is in a different location, adjust `appDirectory` accordingly.

### cpanfile example

Use a `cpanfile` to declare project dependencies for `WithProjectDependencies()`.

```perl
requires 'Mojolicious', '>= 9.0';
requires 'OpenTelemetry::SDK';

on 'test' => sub {
    requires 'Test::More', '>= 1.302190';
};
```

Further reading:
- CPAN::cpanfile reference: https://github.com/miyagawa/cpanfile/blob/master/README.md


## Additional Information

For more info visit <https://aspire.dev/integrations/frameworks/perl/>.

## Roadmap

I'll place a roadmap in Issues to track going forward.

## Additional Examples

I'll create a personal repo with a variety of samples shortly after the first release.

## Feedback & contributing

Please see the main repo for contribution guidelines: <https://github.com/CommunityToolkit/Aspire>.

## Credits

There are many people to thank, but the work of JJAtria in making the OpenTelemetry::SDK module is what makes this integration feel great in Aspire and without it, I don't know that I would have even attempted to create it.  

Thanks also to the Aspire Discord community at large for all the assistance when I had questions about the internals of Aspire.

### Referenced Libraries

This integration references or interacts with the following Perl ecosystem libraries and tools, while the libraries themselves are only installed by individual developers for their projects, I do use them as examples and want to give credit and note their licensing for posterity:

| Resource | Website / Repository | License |
| ---------- | --------------------- | --------- |
| Perl | [perl.org](https://www.perl.org) | [Artistic / GPL](https://github.com/Perl/perl5/blob/blead/Artistic) |
| Strawberry Perl | [strawberryperl.com](https://strawberryperl.com) | [Artistic / GPL](https://github.com/Perl/perl5/blob/blead/Artistic) |
| perlbrew | [perlbrew.pl](https://perlbrew.pl) | [MIT](https://github.com/gugod/App-perlbrew/blob/develop/LICENSE) |
| Berrybrew | [GitHub](https://github.com/stevieb9/berrybrew) | [License](https://github.com/stevieb9/berrybrew?tab=License-1-ov-file#readme) |
| App::cpanminus (cpanm) | [GitHub](https://github.com/miyagawa/cpanminus) | [License](https://metacpan.org/pod/App::cpanminus#LICENSE) |
| Carton | [GitHub](https://github.com/miyagawa/carton) | [License](https://metacpan.org/pod/Carton#LICENSE) |
| local::lib | [metacpan](https://metacpan.org/pod/local::lib) | [License](https://metacpan.org/pod/local::lib#LICENSE) |
| Mojolicious | [mojolicious.org](https://mojolicious.org) | [Artistic-2.0](https://github.com/mojolicious/mojo/blob/main/LICENSE) |
| OpenTelemetry::SDK | [GitHub](https://github.com/jjatria/perl-opentelemetry) | [License](https://github.com/jjatria/perl-opentelemetry?tab=License-1-ov-file#readme) |
| IO::Socket::SSL | [metacpan](https://metacpan.org/pod/IO::Socket::SSL) | [License](https://metacpan.org/pod/IO::Socket::SSL#COPYRIGHT) |
| LWP::UserAgent | [metacpan](https://metacpan.org/pod/LWP::UserAgent) | [License](https://metacpan.org/pod/LWP::UserAgent#COPYRIGHT-AND-LICENSE) |
| Google::ProtocolBuffers::Dynamic | [metacpan](https://metacpan.org/pod/Google::ProtocolBuffers::Dynamic) | [License](https://metacpan.org/pod/Google::ProtocolBuffers::Dynamic#COPYRIGHT-AND-LICENSE) |
