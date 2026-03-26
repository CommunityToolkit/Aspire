# CommunityToolkit.Aspire.Hosting.Perl library

Provides extensions methods and resource definitions for the .NET Aspire AppHost to support running Perl.

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

> **Key insight:** The script path in `AddPerlScript` / `AddPerlApi` is relative to `AppHost.cs`,
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
> `WithLocalLib()` will also currently swap to `cpanm` because it wasn't clear to me at time of release how to integrate it with cpan.

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
