# CommunityToolkit.Aspire.Hosting.Perl library

Provides extensions methods and resource definitions for the Aspire AppHost to support running Perl.

## Using the Perl Hosting Integration

A guide for developers using `CommunityToolkit.Aspire.Hosting.Perl` for the first time, or as a
reference when revisiting the API. This document explains how key hosting API calls map to on-disk
directory layout, environment variable configuration, and runtime behavior.

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

If there are things to install, it should warn you in the dashboard.  You should see links to installation instructions.

See notes about the appDirectory parameter below.

---

## Core Concepts

The integration provides four entry points for adding Perl resources:

| Method | Purpose | Runs |
|--------|---------|------|
| `AddPerlScript(name, appDirectory, scriptName)` | Adds a Perl script (worker, CLI tool, etc.) | `perl -s <scriptName>` |
| `AddPerlApi(name, appDirectory, scriptName)` | Adds a Perl API server (e.g., Mojolicious `daemon`) | `perl <scriptName> daemon` |
| `AddPerlModule(name, appDirectory, moduleName)` | Runs a module's `run` method | `perl -M<moduleName> -e "<moduleName>->run()"` |
| `AddPerlExecutable(name, appDirectory, executablePath)` | Runs a compiled binary or PAR-packed app without the `perl` interpreter | `<executablePath>` |

All four create a `PerlAppResource` that appears in the Aspire dashboard and runs with `appDirectory`
as its working directory. All subsequent configuration methods (`.WithCpanMinus()`, `.WithLocalLib()`,
etc.) chain off the resource builder.

`AddPerlModule` doesn't add `appDirectory` to `@INC`. The module must provide a `run` method and be
installed, under a `WithLocalLib` path, or in a directory you add to `PERL5LIB`.

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
> not to `AppHost.cs`. So are `WithLocalLib` and cpanfile discovery, and the process runs in
> `appDirectory`. Only `appDirectory` itself is resolved against the AppHost project directory.

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

With Carton, `WithLocalLib` also sets `PERL_CARTON_PATH` for the application and its
dependency installer. Carton installs into the same directory used by `PERL5LIB` and
`PERL_LOCAL_LIB_ROOT`; without `WithLocalLib`, that directory defaults to `local`.
The integration leaves `PERL_MM_OPT` and `PERL_MB_OPT` unset for Carton because
Carton manages its own installer options.
Carton's underlying Perl build tools can fail when the local::lib path contains
spaces, even with correct Dockerfile quoting. Prefer a path such as `vendor/perl5`.

### Resolution examples

| `appDirectory` | `WithLocalLib(...)` | Resolved absolute path |
|----------------|---------------------|----------------------|
| `"."` | `"local"` | `<AppHost>/local` |
| `"../scripts"` | `"local"` | `<AppHost>/../scripts/local` |
| `"."` | `"/opt/perl-libs"` | `/opt/perl-libs` (Linux/macOS) |
| `"."` | `"C:\\perl-libs"` | `C:\\perl-libs` (Windows) |

---

## Package Management

While I highly recommend you use cpanm or Carton, the integration aims to support three package managers and two installation strategies:

| Package Manager | Individual Packages | Project Dependencies |
|----------------|-------------------|---------------------|
| **cpan** (default) | ✅ `.WithPackage("Module")` | ❌ Not supported (auto-switches to cpanm when calling `.WithProjectDependencies()`) |
| **cpanm** (App::cpanminus) | ✅ `.WithCpanMinus().WithPackage("Module")` | ✅ `.WithCpanMinus().WithProjectDependencies()` |
| **Carton** | ❌ Not supported | ✅ `.WithCarton().WithProjectDependencies()` |

> The default package manager is `cpan`, but it is automatically switched to `cpanm` when
> `WithProjectDependencies()` is called, since `cpan` does not support `--installdeps`.
> In run mode, every `AddPerl*` method calls `WithProjectDependencies()` for you when `appDirectory`
> contains a `cpanfile`, `Makefile.PL`, or `Build.PL`. Those resources switch to `cpanm` and require it
> to be installed, even if you never call a package-manager method.
> `WithLocalLib()` will also currently swap to `cpanm` because it wasn't clear to me at time of release how to integrate it with cpan.

In run mode, `cpan` is required only by individual-package installers that use the default
package manager. Scripts without dependency installation, cpanm installers, and Carton
installers do not require `cpan`. Each installer checks its final selected command before
starting, using its resolved perlbrew path when configured. Missing commands produce
warnings and installation links; they do not prevent an attempt to start the resource.

In run mode, calling `WithPackage()` repeatedly for the same module on the same resource
reuses one installer and one completion dependency, including for names such as `Foo::Bar`.
The installer keeps the `force` and `skipTest` values from the first call.

Module identities are case-sensitive. If different modules map to the same installer resource
name, `WithPackage()` throws `InvalidOperationException` instead of silently skipping a module.
For example, `Foo88Bar` and `Foo::Bar` both map to `app-Foo88Bar-installer` on a resource named
`app`. Case-only differences also collide because Aspire resource names are case-insensitive.
An installer belonging to another resource is never reused. These checks apply only in run mode;
publish mode retains the module requests without creating local installers.

`WithCarton()` and `WithPackage()` cannot be combined in run or publish mode, in either
call order. Carton dependencies must be declared in `cpanfile`; use `WithProjectDependencies()`
to install them. Individual module installation is supported with cpan or cpanm instead.

---

## Certificate trust

The experimental `WithPerlCertificateTrust()` API (`CTASPIREPERL001`) configures
`SSL_CERT_FILE`, `PERL_LWP_SSL_CA_FILE`, and `MOJO_CA_FILE` for the Perl app and its
package and project dependency installers. Each resource uses the certificate bundle
provided by its own Aspire certificate trust context, not the AppHost process's
`SSL_CERT_FILE` environment variable.

Call it before or after `WithPackage()` or `WithProjectDependencies()`; both orders
are supported. Certificate trust remains opt-in.

---

## OpenTelemetry

Perl resources use OTLP over HTTP with JSON encoding (`http/json`). Perl's OTLP exporter does not
support OTLP over gRPC, and JSON avoids requiring `Google::ProtocolBuffers::Dynamic`.

Configure the dashboard's HTTP OTLP endpoint in run mode, for example with
`ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL` or `DOTNET_DASHBOARD_OTLP_HTTP_ENDPOINT_URL`.
When only the gRPC endpoint is configured, Aspire omits Perl OTLP exporter settings rather than
sending Perl telemetry to the gRPC listener.

The Perl SDK reads `OTEL_PERL_*` variables before standard `OTEL_*` variables. In run mode, the
integration resolves the Perl-specific exporter, endpoint, protocol, and header variables from the
final standard values, so overrides such as `.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", url)`
apply to Perl. Set an `OTEL_PERL_*` variable explicitly to override only the Perl SDK.

---

## Publishing (preview)

When publishing, Perl resources are built as container images. Generated Dockerfile publishing
remains preview until Docker build-and-run smoke tests validate both cpanm and Carton images in CI.

- If the app directory contains a `Dockerfile`, it is used as-is.
- Otherwise, a Dockerfile is generated: cpanm (single-stage) by default, or Carton (multi-stage)
  when `WithCarton()` is used. Base images can be changed with `WithDockerfileBaseImage`.

Generated cpanm Dockerfiles run `cpanm --installdeps --notest .` when the app directory contains
`cpanfile`, `Makefile.PL`, or `Build.PL`. Projects with `Makefile.PL` or `Build.PL` copy the full
application context before installation because these executable manifests can load project-local
files. Projects with only `cpanfile` copy that manifest first, install dependencies, and then copy
the application context, preserving the dependency cache layer when application code changes.
Apps without a dependency manifest skip project dependency installation.
Modules requested with `WithPackage()` are installed with cpanm after project dependencies,
respecting `force`, `skipTest`, and `WithLocalLib()`. Repeated requests for the same module
use the first request's options, matching run mode.

Carton installs dependencies only when a `cpanfile` exists. An existing `cpanfile.snapshot`
is copied; a missing snapshot fails publishing only when deployment mode is enabled.
Deployment mode is the publish default; use `.WithCarton().WithProjectDependencies(cartonDeployment: false)`
to allow installation without a snapshot. Carton requires a `cpanfile` for project dependencies;
use `WithCpanMinus()` for projects with only `Makefile.PL` or `Build.PL`.

Generated Carton Dockerfiles set `PERL_CARTON_PATH` before installing dependencies,
using `/app/local` by default or `/app/<WithLocalLib path>` for a relative custom path.
The runtime image and published environment use the same root for `PERL5LIB` and
`PERL_LOCAL_LIB_ROOT`, so executable entrypoints and child Perl processes can load
installed modules. Generated Perl entrypoint `-I` arguments also use that root.

Generated Dockerfiles normalize local::lib paths within `/app` and reject parent-relative
paths that escape it, such as `../vendor/perl5`. Authored Dockerfiles keep the existing
path resolution instead: rooted paths such as `/opt/perl-libs` are preserved, not remapped
under `/app`. Configure `WithLocalLib` to match the image's dependency installation path.

Generated Dockerfiles install dependencies inside the image and copy the app with `COPY . .`.
To keep host-built modules out of the image, a `<name>.Dockerfile.dockerignore` is emitted next
to the generated Dockerfile. It excludes `local`, any relative `WithLocalLib` path, `blib`, `_build`,
`.git`, `.env` files (at any depth), and Dockerfiles. If the app directory contains an authored `.dockerignore`, no
ignore file is generated and yours applies. Keep `local` (or your local::lib path) in it.

Special characters in a relative `WithLocalLib` path are escaped in the generated ignore rule and quoted in the
generated Dockerfile. Publishing fails with a descriptive error for paths that contain control characters, or that
combine `^` with `*`, `?`, `[`, `]`, or a leading `!`, because Docker cannot match such paths portably.

---

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
