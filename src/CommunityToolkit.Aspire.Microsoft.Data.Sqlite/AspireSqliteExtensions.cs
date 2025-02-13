using Aspire;
using HealthChecks.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Runtime.InteropServices;
using System.Text.Json;
using RuntimeEnvironment = Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for registering Sqlite-related services in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireSqliteExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Sqlite:Client";

    /// <summary>
    /// Registers <see cref="SqliteConnection" /> as scoped in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="SqliteConnectionSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:Sqlite:Client" section.</remarks>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section.</exception>
    public static void AddSqliteConnection(
        this IHostApplicationBuilder builder,
        string name,
        Action<SqliteConnectionSettings>? configureSettings = null) =>
            AddSqliteClient(builder, DefaultConfigSectionName, configureSettings, name, serviceKey: null);

    /// <summary>
    /// Registers <see cref="SqliteConnection" /> as keyed scoped for the given <paramref name="name" /> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The connection name to use to find a connection string.</param>
    /// <param name="configureSettings">An optional method that can be used for customizing the <see cref="SqliteConnectionSettings"/>. It's invoked after the settings are read from the configuration.</param>
    /// <remarks>Reads the configuration from "Aspire:Sqlite:Client" section.</remarks>
    /// <exception cref="InvalidOperationException">If required ConnectionString is not provided in configuration section.</exception>
    public static void AddKeyedSqliteConnection(
        this IHostApplicationBuilder builder,
        string name,
        Action<SqliteConnectionSettings>? configureSettings = null) =>
            AddSqliteClient(builder, $"{DefaultConfigSectionName}:{name}", configureSettings, connectionName: name, serviceKey: name);

    private static void AddSqliteClient(
        this IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<SqliteConnectionSettings>? configureSettings,
        string connectionName,
        object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        SqliteConnectionSettings settings = new();
        var configSection = builder.Configuration.GetSection(configurationSectionName);
        configSection.Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ConnectionString = connectionString;
        }

        if (!string.IsNullOrEmpty(settings.ConnectionString))
        {
            var cbs = new DbConnectionStringBuilder { ConnectionString = settings.ConnectionString };
            if (cbs.TryGetValue("Extensions", out var extensions))
            {
                settings.Extensions = JsonSerializer.Deserialize<IEnumerable<SqliteExtensionMetadata>>((string)extensions) ?? [];
            }
        }

        configureSettings?.Invoke(settings);

        builder.RegisterSqliteServices(settings, connectionName, serviceKey);

        if (!settings.DisableHealthChecks)
        {
            builder.TryAddHealthCheck(new HealthCheckRegistration(
                serviceKey is null ? "Sqlite" : $"Sqlite_{connectionName}",
                sp =>
                {
                    var connection = serviceKey is null
                        ? sp.GetRequiredService<SqliteConnection>()
                        : sp.GetRequiredKeyedService<SqliteConnection>(serviceKey);
                    return new SqliteHealthCheck(
                        new SqliteHealthCheckOptions { ConnectionString = connection.ConnectionString });
                },
                failureStatus: default,
                tags: default,
                timeout: default));
        }
    }

    private static void RegisterSqliteServices(
        this IHostApplicationBuilder builder,
        SqliteConnectionSettings settings,
        string connectionName,
        object? serviceKey)
    {
        if (serviceKey is null)
        {
            builder.Services.AddScoped(sp => CreateConnection(sp, null));
        }
        else
        {
            builder.Services.AddKeyedScoped(serviceKey, CreateConnection);
        }

        SqliteConnection CreateConnection(IServiceProvider sp, object? key)
        {
            var logger = sp.GetRequiredService<ILogger<SqliteConnection>>();
            ConnectionStringValidation.ValidateConnectionString(settings.ConnectionString, connectionName, DefaultConfigSectionName);
            var csb = new DbConnectionStringBuilder { ConnectionString = settings.ConnectionString };
            if (csb.ContainsKey("Extensions"))
            {
                csb.Remove("Extensions");
            }
            var connection = new SqliteConnection(csb.ConnectionString);

            foreach (var extension in settings.Extensions)
            {
                if (extension.IsNuGetPackage)
                {
                    if (string.IsNullOrEmpty(extension.PackageName))
                    {
                        throw new InvalidOperationException("PackageName is required when loading an extension from a NuGet package.");
                    }

                    EnsureLoadableFromNuGet(extension.Extension, extension.PackageName, logger);
                }
                else
                {
                    if (string.IsNullOrEmpty(extension.ExtensionFolder))
                    {
                        throw new InvalidOperationException("ExtensionFolder is required when loading an extension from a folder.");
                    }

                    EnsureLoadableFromLocalPath(extension.Extension, extension.ExtensionFolder);
                }
                connection.LoadExtension(extension.Extension);
            }

            return connection;
        }
    }

    // Adapted from https://github.com/dotnet/docs/blob/dbbeda13bf016a6ff76b0baab1488c927a64ff24/samples/snippets/standard/data/sqlite/ExtensionsSample/Program.cs#L40
    internal static void EnsureLoadableFromNuGet(string package, string library, ILogger<SqliteConnection> logger)
    {
        var runtimeLibrary = DependencyContext.Default?.RuntimeLibraries.FirstOrDefault(l => l.Name == package);
        if (runtimeLibrary is null)
        {
            logger.LogInformation("Could not find the runtime library for package {Package}", package);
            return;
        }

        string sharedLibraryExtension;
        string pathVariableName = "PATH";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            sharedLibraryExtension = ".dll";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            sharedLibraryExtension = ".so";
            pathVariableName = "LD_LIBRARY_PATH";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            sharedLibraryExtension = ".dylib";
            pathVariableName = "DYLD_LIBRARY_PATH";
        }
        else
        {
            throw new NotSupportedException("Unsupported OS platform");
        }

        var candidateAssets = new Dictionary<(string? Package, string Asset), int>();
        var rid = RuntimeEnvironment.GetRuntimeIdentifier();
        var rids = DependencyContext.Default?.RuntimeGraph.First(g => g.Runtime == rid).Fallbacks.ToList() ?? [];
        rids.Insert(0, rid);

        logger.LogInformation("Looking for {Library} in {Package} runtime assets", library, package);
        logger.LogInformation("Possible runtime identifiers: {Rids}", string.Join(", ", rids));

        foreach (var group in runtimeLibrary.NativeLibraryGroups)
        {
            foreach (var file in group.RuntimeFiles)
            {
                if (string.Equals(
                    Path.GetFileName(file.Path),
                    library + sharedLibraryExtension,
                    StringComparison.OrdinalIgnoreCase))
                {
                    var fallbacks = rids.IndexOf(group.Runtime);
                    if (fallbacks != -1)
                    {
                        logger.LogInformation("Found {Library} in {Package} runtime assets at {Path}", library, package, file.Path);
                        candidateAssets.Add((runtimeLibrary.Path, file.Path), fallbacks);
                    }
                }
            }
        }

        var assetPath = candidateAssets
            .OrderBy(p => p.Value)
            .Select(p => p.Key)
            .FirstOrDefault();
        if (assetPath != default)
        {
            string? assetDirectory = null;
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, assetPath.Asset)))
            {
                // NB: Framework-dependent deployments copy assets to the application base directory
                assetDirectory = Path.Combine(
                    AppContext.BaseDirectory,
                    Path.GetDirectoryName(assetPath.Asset.Replace('/', Path.DirectorySeparatorChar))!);

                logger.LogInformation("Found {Library} in {Package} runtime assets at {Path}", library, package, assetPath.Asset);
            }
            else
            {
                string? assetFullPath = null;
                var probingDirectories = ((string?)AppDomain.CurrentDomain.GetData("PROBING_DIRECTORIES"))?
                    .Split(Path.PathSeparator) ?? [];
                foreach (var directory in probingDirectories)
                {
                    var candidateFullPath = Path.Combine(
                        directory,
                        assetPath.Package ?? "",
                        assetPath.Asset);
                    if (File.Exists(candidateFullPath))
                    {
                        assetFullPath = candidateFullPath;
                    }
                }

                assetDirectory = Path.GetDirectoryName(assetFullPath);
                logger.LogInformation("Found {Library} in {Package} runtime assets at {Path} (using PROBING_DIRECTORIES: {ProbingDirectories})", library, package, assetFullPath, string.Join(",", probingDirectories));
            }

            var path = new HashSet<string>(Environment.GetEnvironmentVariable(pathVariableName)!.Split(Path.PathSeparator));

            if (assetDirectory is not null && path.Add(assetDirectory))
            {
                logger.LogInformation("Adding {AssetDirectory} to {PathVariableName}", assetDirectory, pathVariableName);
                Environment.SetEnvironmentVariable(pathVariableName, string.Join(Path.PathSeparator, path));
                logger.LogInformation("Set {PathVariableName} to: {PathVariableValue}", pathVariableName, Environment.GetEnvironmentVariable(pathVariableName));
            }
        }
        else
        {
            logger.LogInformation("Could not find {Library} in {Package} runtime assets", library, package);
        }
    }

    internal static void EnsureLoadableFromLocalPath(string library, string assetDirectory)
    {
        string sharedLibraryExtension;
        string pathVariableName = "PATH";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            sharedLibraryExtension = ".dll";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            sharedLibraryExtension = ".so";
            pathVariableName = "LD_LIBRARY_PATH";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            sharedLibraryExtension = ".dylib";
            pathVariableName = "DYLD_LIBRARY_PATH";
        }
        else
        {
            throw new NotSupportedException("Unsupported OS platform");
        }

        if (File.Exists(Path.Combine(assetDirectory, library + sharedLibraryExtension)))
        {
            var path = new HashSet<string>(Environment.GetEnvironmentVariable(pathVariableName)!.Split(Path.PathSeparator));

            if (assetDirectory is not null && path.Add(assetDirectory))
                Environment.SetEnvironmentVariable(pathVariableName, string.Join(Path.PathSeparator, path));
        }
    }
}
