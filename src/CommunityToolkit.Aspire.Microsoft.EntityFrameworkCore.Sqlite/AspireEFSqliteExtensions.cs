using Aspire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
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

            var csb = new DbConnectionStringBuilder { ConnectionString = settings.ConnectionString };
            if (csb.ContainsKey("Extensions"))
            {
                csb.Remove("Extensions");
            }

            dbContextOptionsBuilder.UseSqlite(csb.ConnectionString);
            configureDbContextOptions?.Invoke(dbContextOptionsBuilder);
        }
    }
}
