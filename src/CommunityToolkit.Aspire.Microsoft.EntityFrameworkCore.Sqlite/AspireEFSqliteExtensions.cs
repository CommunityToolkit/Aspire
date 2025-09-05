using Aspire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        if (!settings.DisableHealthChecks)
        {
            builder.TryAddHealthCheck(name: typeof(TContext).Name, static hcBuilder => hcBuilder.AddDbContextCheck<TContext>());
        }

        void ConfigureDbContext(DbContextOptionsBuilder dbContextOptionsBuilder)
        {
            // delay validating the ConnectionString until the DbContext is requested. This ensures an exception doesn't happen until a Logger is established.
            ConnectionStringValidation.ValidateConnectionString(settings.ConnectionString, name, DefaultConfigSectionName, $"{DefaultConfigSectionName}:{typeof(TContext).Name}", isEfDesignTime: EF.IsDesignTime);

            dbContextOptionsBuilder.UseSqlite(settings.ConnectionString);
            configureDbContextOptions?.Invoke(dbContextOptionsBuilder);
        }
    }
}

namespace Microsoft.AspNetCore.Builder;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Extension methods for configuring Sqlite with Entity Framework Core on WebApplicationBuilder.
/// </summary>
public static class AspireEFSqliteWebExtensions
{
    /// <summary>
    /// Enriches a <see cref="WebApplicationBuilder"/> to register the <typeparamref name="TDbContext"/> as a scoped service 
    /// with simplified configuration and optional OpenTelemetry instrumentation.
    /// </summary>
    /// <typeparam name="TDbContext">The type of the <see cref="DbContext"/>.</typeparam>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> to read config from and add services to.</param>
    /// <param name="connectionStringName">The name used to retrieve the connection string from the ConnectionStrings configuration section. Defaults to "DefaultConnection".</param>
    /// <param name="enableOpenTelemetry">Whether to enable OpenTelemetry instrumentation for Entity Framework Core. Defaults to true.</param>
    /// <returns>The <see cref="WebApplicationBuilder"/> so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown if mandatory <paramref name="builder"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the connection string is not found or is empty.</exception>
    public static WebApplicationBuilder EnrichSqliteDatabaseDbContext<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TDbContext>(
        this WebApplicationBuilder builder,
        string? connectionStringName = "DefaultConnection",
        bool enableOpenTelemetry = true)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionStringName);

        var connectionString = builder.Configuration.GetConnectionString(connectionStringName);
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{connectionStringName}' not found or empty.");
        }

        builder.Services.AddDbContext<TDbContext>(options =>
            options.UseSqlite(connectionString));

        // TODO: Add OpenTelemetry support once we can verify package references work
        // if (enableOpenTelemetry)
        // {
        //     builder.Services.AddOpenTelemetry()
        //         .WithTracing(tracing => tracing
        //             .AddEntityFrameworkCoreInstrumentation());
        // }

        return builder;
    }
}
