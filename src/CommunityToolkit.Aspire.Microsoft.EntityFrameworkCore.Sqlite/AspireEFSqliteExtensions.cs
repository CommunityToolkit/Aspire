using Aspire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring Sqlite with Entity Framework Core.
/// </summary>
public static class AspireEFSqliteExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Sqlite:EntityFrameworkCore:Sqlite";
    private const DynamicallyAccessedMemberTypes RequiredByEF = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties;

    /// <summary>
    /// Registers the <typeparamref name="TContext"/> as a scoped service in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <typeparam name="TContext">The type of the <see cref="DbContext"/>.</typeparam>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to read config from and add services to.</param>
    /// <param name="name">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <param name="configureDbContextOptions">An optional method that can be used for customizing the <see cref="DbContextOptionsBuilder{TContext}"/>.</param> 
    /// <remarks>
    /// <para>
    /// Reads the configuration from "Aspire:Sqlite:EntityFrameworkCore:Sqlite:{typeof(TContext).Name}" config section, or "Aspire:Sqlite:EntityFrameworkCore:Sqlite" if former does not exist.
    /// </para>
    /// <para>
    /// The <see cref="DbContext.OnConfiguring" /> method can then be overridden to configure <see cref="DbContext" /> options.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if mandatory <paramref name="builder"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when mandatory <see cref="SqliteEntityFrameworkCoreSettings.ConnectionString"/> is not provided.</exception>
    public static void AddSqliteDbContext<[DynamicallyAccessedMembers(RequiredByEF)] TContext>(
        this IHostApplicationBuilder builder,
        string name,
        Action<SqliteEntityFrameworkCoreSettings>? configureSettings = null,
        Action<DbContextOptionsBuilder>? configureDbContextOptions = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.EnsureDbContextNotRegistered<TContext>();

        var settings = builder.GetDbContextSettings<TContext, SqliteEntityFrameworkCoreSettings>(
            DefaultConfigSectionName,
            (settings, section) => section.Bind(settings)
        );

        if (builder.Configuration.GetConnectionString(name) is string connectionString)
        {
            settings.ConnectionString = connectionString;
        }

        configureSettings?.Invoke(settings);

        builder.Services.AddDbContextPool<TContext>(ConfigureDbContext);

        ConfigureInstrumentation<TContext>(builder, settings);

        void ConfigureDbContext(DbContextOptionsBuilder dbContextOptionsBuilder)
        {
            // delay validating the ConnectionString until the DbContext is requested. This ensures an exception doesn't happen until a Logger is established.
            ConnectionStringValidation.ValidateConnectionString(settings.ConnectionString, name, DefaultConfigSectionName, $"{DefaultConfigSectionName}:{typeof(TContext).Name}", isEfDesignTime: EF.IsDesignTime);

            dbContextOptionsBuilder.UseSqlite(settings.ConnectionString);
            configureDbContextOptions?.Invoke(dbContextOptionsBuilder);
        }
    }

    /// <summary>
    /// Enriches a <see cref="IHostApplicationBuilder"/> to register the <typeparamref name="TDbContext"/> as a scoped service 
    /// with simplified configuration and optional OpenTelemetry instrumentation.
    /// </summary>
    /// <typeparam name="TDbContext">The type of the <see cref="DbContext"/>.</typeparam>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to read config from and add services to.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown if mandatory <paramref name="builder"/> is null.</exception>
    public static void EnrichSqliteDatabaseDbContext<[DynamicallyAccessedMembers(RequiredByEF)] TDbContext>(
        this IHostApplicationBuilder builder,
        Action<SqliteEntityFrameworkCoreSettings>? configureSettings = null)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        var settings = builder.GetDbContextSettings<TDbContext, SqliteEntityFrameworkCoreSettings>(
            DefaultConfigSectionName,
            null,
            (settings, section) => section.Bind(settings)
        );

        configureSettings?.Invoke(settings);

        builder.Services.AddDbContext<TDbContext>(options =>
            options.UseSqlite(settings.ConnectionString));
        ConfigureInstrumentation<TDbContext>(builder, settings);
    }

    private static void ConfigureInstrumentation<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TDbContext>(IHostApplicationBuilder builder, SqliteEntityFrameworkCoreSettings settings) where TDbContext : DbContext
    {
        if (!settings.DisableTracing)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .AddEntityFrameworkCoreInstrumentation());
        }

        if (!settings.DisableHealthChecks)
        {
            builder.TryAddHealthCheck(
                name: typeof(TDbContext).Name,
                static hcBuilder => hcBuilder.AddDbContextCheck<TDbContext>());
        }
    }

    internal static TSettings GetDbContextSettings<TContext, TSettings>(this IHostApplicationBuilder builder, string defaultConfigSectionName, string? connectionName, Action<TSettings, IConfiguration> bindSettings)
        where TSettings : new()
    {
        TSettings settings = new();
        var configurationSection = builder.Configuration.GetSection(defaultConfigSectionName);
        bindSettings(settings, configurationSection);
        // If the connectionName is not provided, we've been called in the context
        // of an Enrich invocation and don't need to bind the connectionName specific settings.
        // Instead, we'll just bind to the TContext-specific settings.
        if (connectionName is not null)
        {
            var connectionSpecificConfigurationSection = configurationSection.GetSection(connectionName);
            bindSettings(settings, connectionSpecificConfigurationSection);
        }
        var typeSpecificConfigurationSection = configurationSection.GetSection(typeof(TContext).Name);
        if (typeSpecificConfigurationSection.Exists()) // https://github.com/dotnet/runtime/issues/91380
        {
            bindSettings(settings, typeSpecificConfigurationSection);
        }

        return settings;
    }
}
