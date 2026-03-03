using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Perl application resource.
/// </summary>
/// <remarks>
/// <para>
/// This resource allows Perl applications (scripts, web servers, APIs, background services) to run as part
/// of a distributed application. The resource manages the Perl executable, working directory,
/// and lifecycle of the Perl application.
/// </para>
/// <para>
/// Perl applications can expose HTTP endpoints, communicate with other services, and participate
/// in service discovery like other Aspire resources. They support automatic OpenTelemetry instrumentation
/// for observability when configured with the appropriate Perl packages.
/// </para>
/// <para>
/// This resource supports various Perl execution environments including:
/// <list type="bullet">
/// <item>System Perl installations</item>
/// <item>User specified Perl environments</item>
/// <item>Local::Lib environments</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// Add a Perl web application using Mojolicious or Dancer2:
/// <code lang="csharp">
/// var builder = DistributedApplication.CreateBuilder(args);
/// 
/// var perl = builder.AddPerlApp("api", "../perl-api", "app.pl")
///     .WithHttpEndpoint(port: 5000)
///     .WithArgs("--host", "0.0.0.0");
/// 
/// builder.AddProject&lt;Projects.Frontend&gt;("frontend")
///     .WithReference(perl);
/// 
/// builder.Build().Run();
/// </code>
/// </example>
/// <param name="name">The name of the resource in the application model.</param>
/// <param name="executablePath">
/// The path to the Perl executable. This can be:
/// <list type="bullet">
/// <item>An absolute path: "/usr/bin/perl"</item>
/// <item>A relative path: "./local/bin/perl"</item>
/// <item>A command on the PATH: "perl" or "myperl"</item>
/// </list>
/// The executable is typically located in a virtual environment's bin (Linux/macOS) or Scripts (Windows) directory.
/// </param>
/// <param name="appDirectory">
/// The working directory for the Perl application. Perl scripts and modules
/// will be resolved relative to this directory. This is typically the root directory
/// of your Perl project containing your main script and any local modules.
/// </param>
public class PerlAppResource(string name, string executablePath, string appDirectory)
    : ExecutableResource(name, executablePath, appDirectory), IResourceWithServiceDiscovery;
