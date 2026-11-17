using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable ASPIRECERTIFICATES001

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

// SSL_CERT_FILE is process-wide, so these tests must not overlap other collections.
[CollectionDefinition(nameof(WithCertificateTrustTests), DisableParallelization = true)]
public class WithCertificateTrustTestsCollection;

[Collection(nameof(WithCertificateTrustTests))]
public class WithCertificateTrustTests
{
    private const string OtlpCertificate = "OTEL_EXPORTER_OTLP_CERTIFICATE";
    private const string PerlOtlpCertificate = "OTEL_PERL_EXPORTER_OTLP_CERTIFICATE";

    private static readonly string[] PerlTlsVariables = ["SSL_CERT_FILE", "PERL_LWP_SSL_CA_FILE", "MOJO_CA_FILE"];

    [Theory]
    [InlineData("script")]
    [InlineData("api")]
    [InlineData("module")]
    [InlineData("executable")]
    public async Task AddPerl_DefaultCertificateTrust_UsesSystemScopeAndSetsPerlVariables(string entrypoint)
    {
        var builder = DistributedApplication.CreateBuilder();
        var perlApp = entrypoint switch
        {
            "script" => builder.AddPerlScript("perl-app", "scripts", "app.pl"),
            "api" => builder.AddPerlApi("perl-app", "scripts", "app.pl"),
            "module" => builder.AddPerlModule("perl-app", "lib", "App::Main"),
            "executable" => builder.AddPerlExecutable("perl-app", "bin", "app"),
            _ => throw new ArgumentOutOfRangeException(nameof(entrypoint))
        };

        using var app = builder.Build();

        var (environment, data) = await BuildCertificateTrustConfigurationAsync(perlApp.Resource, app.Services);

        Assert.Equal(CertificateTrustScope.System, data.Scope);
        var bundlePath = GetBundlePath(perlApp.Resource);
        foreach (var variable in PerlTlsVariables.Append(OtlpCertificate))
        {
            AssertVariable(environment, variable, bundlePath);
        }
    }

    [Fact]
    public async Task DefaultCertificateTrust_OverrideScope_SetsPerlTlsAndOtlpVariables()
    {
        var builder = DistributedApplication.CreateBuilder();
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var bundlePath = CreateBundleExpression(perlApp.Resource);
        var environment = await InvokeCertificateTrustCallbacksAsync(
            perlApp.Resource, CreateRunContext(app), CertificateTrustScope.Override, bundlePath);

        foreach (var variable in PerlTlsVariables.Append(OtlpCertificate))
        {
            Assert.True(environment.TryGetValue(variable, out var value), $"Missing {variable}.");
            Assert.Same(bundlePath, value);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultCertificateTrust_AppendScope_AppliesOnlyOtlpTrustAndLogs(bool useProjectInstaller)
    {
        var builder = DistributedApplication.CreateBuilder();
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus()
            .WithCertificateTrustScope(CertificateTrustScope.Append);

        if (useProjectInstaller)
        {
            perlApp.WithProjectDependencies();
        }
        else
        {
            perlApp.WithPackage("Mojolicious");
        }

        using var app = builder.Build();
        await LoggingTestHelper.PublishBeforeStartAsync(builder, app);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installer = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
        var executionContext = CreateRunContext(app);

        foreach (var resource in new IResource[] { perlApp.Resource, installer })
        {
            // Invoke directly so missing developer certificates cannot skip the Append regression.
            var bundlePath = CreateBundleExpression(resource);
            var environment = await InvokeCertificateTrustCallbacksAsync(
                resource, executionContext, CertificateTrustScope.Append, bundlePath);

            Assert.True(environment.TryGetValue(OtlpCertificate, out var otlpCertificate), $"Missing {OtlpCertificate}.");
            Assert.Same(bundlePath, otlpCertificate);
            foreach (var variable in PerlTlsVariables)
            {
                Assert.DoesNotContain(variable, environment.Keys);
            }

            var logs = await LoggingTestHelper.CollectLogsAsync(app, resource.Name);
            Assert.Contains(logs, line =>
                line.Contains("Append", StringComparison.Ordinal) &&
                line.Contains("OTLP", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task DefaultCertificateTrust_WithOtlpEndpoint_MirrorsPerlOtlpCertificate()
    {
        var builder = CreateOtlpBuilder();
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var (environment, _) = await BuildCertificateTrustConfigurationAsync(perlApp.Resource, app.Services);

        var bundlePath = GetBundlePath(perlApp.Resource);
        AssertVariable(environment, OtlpCertificate, bundlePath);
        AssertVariable(environment, PerlOtlpCertificate, bundlePath);
    }

    [Fact]
    public async Task DefaultCertificateTrust_ExplicitPerlOtlpCertificate_IsPreserved()
    {
        const string customCertificate = "/custom/ca.pem";
        var builder = CreateOtlpBuilder();
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithEnvironment(PerlOtlpCertificate, customCertificate);

        using var app = builder.Build();

        var (environment, _) = await BuildCertificateTrustConfigurationAsync(perlApp.Resource, app.Services);

        AssertVariable(environment, OtlpCertificate, GetBundlePath(perlApp.Resource));
        AssertVariable(environment, PerlOtlpCertificate, customCertificate);
    }

    public static TheoryData<CertificateTrustScope?, bool, bool> InstallerScopeCases()
    {
        var data = new TheoryData<CertificateTrustScope?, bool, bool>();
        foreach (var useProjectInstaller in new[] { false, true })
        {
            data.Add(null, true, useProjectInstaller);
            foreach (var scope in new[] { CertificateTrustScope.None, CertificateTrustScope.Append, CertificateTrustScope.Override })
            {
                data.Add(scope, true, useProjectInstaller);
                data.Add(scope, false, useProjectInstaller);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(InstallerScopeCases))]
    public async Task Installers_InheritParentCertificateTrustScope(
        CertificateTrustScope? parentScope,
        bool scopeBeforeInstaller,
        bool useProjectInstaller)
    {
        var builder = DistributedApplication.CreateBuilder();
        using var certificate = CreateCertificateAuthority();
        var authorities = builder.AddCertificateAuthorityCollection("custom-ca")
            .WithCertificate(certificate);
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus()
            .WithCertificateAuthorityCollection(authorities);

        if (scopeBeforeInstaller && parentScope is { } earlyScope)
        {
            perlApp.WithCertificateTrustScope(earlyScope);
        }

        if (useProjectInstaller)
        {
            perlApp.WithProjectDependencies();
        }
        else
        {
            perlApp.WithPackage("Mojolicious");
        }

        if (!scopeBeforeInstaller && parentScope is { } lateScope)
        {
            perlApp.WithCertificateTrustScope(lateScope);
        }

        using var app = builder.Build();
        await LoggingTestHelper.PublishBeforeStartAsync(builder, app);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installer = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());

        var (environment, data) = await BuildCertificateTrustConfigurationAsync(installer, app.Services);

        var expectedScope = parentScope ?? CertificateTrustScope.System;
        Assert.Equal(expectedScope, data.Scope);
        if (expectedScope is CertificateTrustScope.System or CertificateTrustScope.Override)
        {
            var bundlePath = GetBundlePath(installer);
            foreach (var variable in PerlTlsVariables.Append(OtlpCertificate))
            {
                AssertVariable(environment, variable, bundlePath);
            }
        }
        else
        {
            foreach (var variable in PerlTlsVariables)
            {
                Assert.DoesNotContain(variable, environment.Keys);
            }

            if (expectedScope == CertificateTrustScope.Append)
            {
                AssertVariable(environment, OtlpCertificate, GetBundlePath(installer));
            }
            else
            {
                Assert.DoesNotContain(OtlpCertificate, environment.Keys);
            }
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Installers_InheritParentCertificateAuthoritiesAndDeveloperTrust(
        bool trustBeforeInstaller,
        bool useProjectInstaller)
    {
        var builder = DistributedApplication.CreateBuilder();
        using var certificate = CreateCertificateAuthority();
        var authorities = builder.AddCertificateAuthorityCollection("custom-ca")
            .WithCertificate(certificate);
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus()
            .WithCertificateTrustScope(CertificateTrustScope.Override);

        if (trustBeforeInstaller)
        {
            perlApp.WithCertificateAuthorityCollection(authorities)
                .WithDeveloperCertificateTrust(false);
        }

        if (useProjectInstaller)
        {
            perlApp.WithProjectDependencies();
        }
        else
        {
            perlApp.WithPackage("Mojolicious");
        }

        if (!trustBeforeInstaller)
        {
            perlApp.WithCertificateAuthorityCollection(authorities)
                .WithDeveloperCertificateTrust(false);
        }

        using var app = builder.Build();
        await LoggingTestHelper.PublishBeforeStartAsync(builder, app);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installer = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());

        var (_, data) = await BuildCertificateTrustConfigurationAsync(installer, app.Services);

        Assert.Equal(CertificateTrustScope.Override, data.Scope);
        Assert.Equal(certificate.Thumbprint, Assert.Single(data.Certificates).Thumbprint);
        Assert.True(installer.TryGetLastAnnotation<CertificateAuthorityCollectionAnnotation>(out var annotation));
        Assert.False(annotation.TrustDeveloperCertificates);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "shell-ca-bundle.pem")]
    [InlineData(true, "shell-ca-bundle.pem")]
    public async Task DefaultCertificateTrust_InstallersUseTheirCertificateContext(
        bool useProjectInstaller,
        string? processCertificateFile)
    {
        var originalCertificateFile = Environment.GetEnvironmentVariable("SSL_CERT_FILE");
        try
        {
            Environment.SetEnvironmentVariable("SSL_CERT_FILE", processCertificateFile);
            Assert.Equal(processCertificateFile, Environment.GetEnvironmentVariable("SSL_CERT_FILE"));

            var builder = DistributedApplication.CreateBuilder();
            var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl")
                .WithCpanMinus();

            if (useProjectInstaller)
            {
                perlApp.WithProjectDependencies();
            }
            else
            {
                perlApp.WithPackage("Mojolicious");
            }

            using var app = builder.Build();
            await LoggingTestHelper.PublishBeforeStartAsync(builder, app);

            var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
            var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
            var executionContext = CreateRunContext(app);

            foreach (var resource in new IResource[] { perlApp.Resource, installerResource })
            {
                var bundlePath = CreateBundleExpression(resource);
                var environment = await InvokeCertificateTrustCallbacksAsync(
                    resource, executionContext, CertificateTrustScope.Override, bundlePath);

                // Deliberately reverse DCP's order to catch shell values overwriting resource-specific trust.
                var environmentContext = new EnvironmentCallbackContext(executionContext, environment);
                foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                {
                    await callback.Callback(environmentContext);
                }

                Assert.All(PerlTlsVariables, variable =>
                {
                    Assert.True(environment.TryGetValue(variable, out var value), $"{resource.Name} is missing {variable}.");
                    Assert.Same(bundlePath, value);
                });
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("SSL_CERT_FILE", originalCertificateFile);
        }
    }

    [Fact]
    public void WithPerlCertificateTrust_ShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<PerlAppResource> builder = null!;

#pragma warning disable CTASPIREPERL001, CS0618
        Assert.Throws<ArgumentNullException>(() => builder.WithPerlCertificateTrust());
#pragma warning restore CTASPIREPERL001, CS0618
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithPerlCertificateTrust_IsNoOpOverDefaults(bool certificateTrustFirst)
    {
        var builder = DistributedApplication.CreateBuilder();
        var perlApp = builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus();

#pragma warning disable CTASPIREPERL001, CS0618
        if (certificateTrustFirst)
        {
            perlApp.WithPerlCertificateTrust();
        }

        perlApp.WithPackage("Mojolicious")
            .WithProjectDependencies()
            .WithPerlCertificateTrust()
            .WithPerlCertificateTrust();
#pragma warning restore CTASPIREPERL001, CS0618

        using var app = builder.Build();
        await LoggingTestHelper.PublishBeforeStartAsync(builder, app);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.Single(appModel.Resources.OfType<PerlAppResource>());
        Assert.Equal(2, appModel.Resources.OfType<PerlModuleInstallerResource>().Count());
        foreach (var resource in appModel.Resources)
        {
            Assert.Single(resource.Annotations.OfType<CertificateTrustConfigurationCallbackAnnotation>());

            var (_, data) = await BuildCertificateTrustConfigurationAsync(resource, app.Services);
            Assert.Equal(CertificateTrustScope.System, data.Scope);
        }
    }

    private static IDistributedApplicationBuilder CreateOtlpBuilder()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"] = "http://localhost:4317";
        builder.Configuration["ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL"] = "http://localhost:4318";
        builder.Configuration["DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"] = null;
        builder.Configuration["DOTNET_DASHBOARD_OTLP_HTTP_ENDPOINT_URL"] = null;
        return builder;
    }

    private static DistributedApplicationExecutionContext CreateRunContext(DistributedApplication app) =>
        new(new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)
        {
            Services = app.Services
        });

    private static X509Certificate2 CreateCertificateAuthority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Aspire Perl certificate tests", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        var now = DateTimeOffset.UtcNow;
        return request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
    }

    private static string GetCertificateDirectory(IResource resource) =>
        Path.Combine(Path.GetTempPath(), "aspire-perl-certificate-tests", resource.Name, "certs");

    private static string GetBundlePath(IResource resource) =>
        Path.Combine(GetCertificateDirectory(resource), "bundle.pem");

    private static ReferenceExpression CreateBundleExpression(IResource resource) =>
        ReferenceExpression.Create($"{GetBundlePath(resource)}");

    private static void AssertVariable(Dictionary<string, string> environment, string variable, string expected)
    {
        Assert.True(environment.TryGetValue(variable, out var value), $"Missing {variable}.");
        Assert.Equal(expected, value);
    }

    private static async Task<Dictionary<string, object>> InvokeCertificateTrustCallbacksAsync(
        IResource resource,
        DistributedApplicationExecutionContext executionContext,
        CertificateTrustScope scope,
        ReferenceExpression bundlePath)
    {
        var environment = new Dictionary<string, object>();
        var certificateDirectory = GetCertificateDirectory(resource);
        var context = new CertificateTrustConfigurationCallbackAnnotationContext
        {
            ExecutionContext = executionContext,
            Resource = resource,
            Arguments = [],
            EnvironmentVariables = environment,
            CertificateBundlePath = bundlePath,
            CertificateDirectoriesPath = ReferenceExpression.Create($"{certificateDirectory}"),
            Scope = scope,
            CancellationToken = TestContext.Current.CancellationToken
        };

        foreach (var callback in resource.Annotations.OfType<CertificateTrustConfigurationCallbackAnnotation>())
        {
            await callback.Callback(context);
        }

        return environment;
    }

    /// <summary>
    /// Runs the same environment and certificate-trust gatherers DCP uses, so scope resolution,
    /// Aspire's own SSL_* variables, and final OTLP certificate values are all exercised together.
    /// </summary>
    private static async Task<(Dictionary<string, string> Environment, CertificateTrustExecutionConfigurationData Data)>
        BuildCertificateTrustConfigurationAsync(IResource resource, IServiceProvider services)
    {
        var executionContext = new DistributedApplicationExecutionContext(
            new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)
            {
                Services = services
            });
        var certificateDirectory = GetCertificateDirectory(resource);

        var result = await ExecutionConfigurationBuilder.Create(resource)
            .WithEnvironmentVariablesConfig()
            .WithCertificateTrustConfig(_ => new CertificateTrustExecutionConfigurationContext
            {
                CertificateBundlePath = CreateBundleExpression(resource),
                CertificateDirectoriesPath = ReferenceExpression.Create($"{certificateDirectory}"),
                RootCertificatesPath = certificateDirectory
            })
            .BuildAsync(executionContext, NullLogger.Instance, TestContext.Current.CancellationToken);

        if (result.Exception is not null)
        {
            ExceptionDispatchInfo.Throw(result.Exception);
        }

        Assert.True(result.TryGetAdditionalData<CertificateTrustExecutionConfigurationData>(out var data));
        return (result.EnvironmentVariables.ToDictionary(), data);
    }
}

#pragma warning restore ASPIRECERTIFICATES001
