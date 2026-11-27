#pragma warning disable CTASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

public class SqlProjectPipelineStepTests
{
    [Fact]
    public void AddSqlProjectRegistersComposableStepsByDefault()
    {
        var builder = CreateBuilder();
        var resource = AddSqlProjectFromPath(builder);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var sqlProject = Assert.Single(model.Resources.OfType<SqlProjectResource>());
        Assert.True(sqlProject.TryGetAnnotationsOfType<PipelineStepAnnotation>(out var annotations));
        Assert.Single(annotations);
        Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options));

        var steps = SqlProjectPipelineSteps.CreateSteps(sqlProject, options.Options).ToList();

        Assert.Equal(
            ["build-schema", "publish-schema", "report-schema", "script-schema", "deploy-schema"],
            steps.Select(step => step.Name));
        Assert.DoesNotContain(steps, step => step.RequiredBySteps.Count > 0);
        Assert.Equal(["build-schema"], steps.Single(step => step.Name == "publish-schema").DependsOnSteps);
        Assert.Contains(WellKnownPipelineSteps.ProcessParameters, steps.Single(step => step.Name == "report-schema").DependsOnSteps);
        Assert.Same(resource.Resource, sqlProject);
    }

    [Fact]
    public void WithDacpacPipelineSeparatelyControlsAggregateSteps()
    {
        var builder = CreateBuilder();
        var resource = AddSqlProjectFromPath(builder)
            .WithPipelineOptions(new()
            {
                IncludeInPublish = true,
                IncludeInDeploy = true
            });

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var sqlProject = Assert.Single(model.Resources.OfType<SqlProjectResource>());
        Assert.Same(resource.Resource, sqlProject);
        Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var annotation));

        var steps = SqlProjectPipelineSteps.CreateSteps(sqlProject, annotation.Options).ToList();

        Assert.Contains(WellKnownPipelineSteps.Publish, steps.Single(step => step.Name == "publish-schema").RequiredBySteps);
        Assert.Contains(WellKnownPipelineSteps.Deploy, steps.Single(step => step.Name == "deploy-schema").RequiredBySteps);
        Assert.DoesNotContain(steps, step =>
            step.Name is "report-schema" or "script-schema"
            && step.RequiredBySteps.Count > 0);
    }

    [Fact]
    public void WithDacpacPipelineMergesRepeatedCallsWithoutResettingExplicitFalse()
    {
        var builder = CreateBuilder();
        var resource = AddSqlProjectFromPath(builder)
            .WithPipelineOptions(new()
            {
                IncludeInPublish = true,
                IncludeInDeploy = true,
                BuildConfiguration = "Production",
                OutputDirectory = "dacpac-output",
                SqlPackageVersion = "170.2.70",
                TargetConnectionStringParameterName = "schema-target"
            })
            .WithPipelineOptions(new()
            {
                IncludeInPublish = false
            });

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var sqlProject = Assert.Single(model.Resources.OfType<SqlProjectResource>());
        Assert.Same(resource.Resource, sqlProject);
        Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var annotation));

        Assert.False(annotation.Options.IncludeInPublish);
        Assert.True(annotation.Options.IncludeInDeploy);
        Assert.Equal("Production", annotation.Options.BuildConfiguration);
        Assert.Equal("dacpac-output", annotation.Options.OutputDirectory);
        Assert.Equal("170.2.70", annotation.Options.SqlPackageVersion);
        Assert.Equal("schema-target", annotation.Options.TargetConnectionStringParameterName);
    }

    [Fact]
    public void FileSourceUsesParametersInsteadOfBuildStep()
    {
        var builder = CreateBuilder();
        builder.Configuration["DacpacPipeline:schema:Source"] = "File";
        builder.Configuration["DacpacPipeline:schema:IncludeInPublish"] = "false";
        AddSqlProjectFromPath(builder).WithPipelineOptions(new()
        {
            IncludeInPublish = true
        });

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var sqlProject = Assert.Single(model.Resources.OfType<SqlProjectResource>());
        Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var annotation));

        var steps = SqlProjectPipelineSteps.CreateSteps(sqlProject, annotation.Options).ToList();

        Assert.Contains(steps, step => step.Name == "build-schema");
        Assert.All(steps.Where(step => step.Name != "build-schema"), step =>
            Assert.DoesNotContain("build-schema", step.DependsOnSteps));
        Assert.All(steps.Where(step => step.Name != "build-schema"), step =>
            Assert.Contains(WellKnownPipelineSteps.ProcessParameters, step.DependsOnSteps));
        Assert.Equal(DacpacSource.File, annotation.Options.Source);
        Assert.False(annotation.Options.IncludeInPublish);
        Assert.All(steps, step => Assert.Empty(step.RequiredBySteps));
    }

    [Fact]
    public void PackageResourceDoesNotAcceptSourceOverride()
    {
        var builder = CreateBuilder();
        var package = builder.AddSqlPackage("schema", "Contoso.Database", "1.2.3");

        package.WithPipelineOptions();
        var exception = Assert.Throws<DistributedApplicationException>(() =>
            package.WithPipelineOptions(new() { Source = DacpacSource.File }));

        Assert.Contains("package resource", exception.Message);
    }

    [Fact]
    public void PackageResourceValidatesConfigurationOverrideAfterMetadataIsAvailable()
    {
        var builder = CreateBuilder();
        builder.Configuration["DacpacPipeline:schema:Source"] = "File";
        builder.AddSqlPackage("schema", "Contoso.Database", "1.2.3");

        using var app = builder.Build();
        var sqlProject = Assert.Single(
            app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<SqlProjectResource>());
        Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var annotation));

        var exception = Assert.Throws<DistributedApplicationException>(
            () => SqlProjectPipelineSteps.CreateSteps(sqlProject, annotation.Options).ToList());

        Assert.Contains("package resource", exception.Message);
    }

    [Fact]
    public void BuildSourceRequiresSqlProjectMetadata()
    {
        var builder = CreateBuilder();
        var resource = builder.AddSqlProject("schema").WithDacpac("schema.dacpac");

        resource.WithPipelineOptions(new() { Source = DacpacSource.Build });
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var sqlProject = Assert.Single(model.Resources.OfType<SqlProjectResource>());
        Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var annotation));

        var exception = Assert.Throws<DistributedApplicationException>(
            () => SqlProjectPipelineSteps.CreateSteps(sqlProject, annotation.Options).ToList());
        Assert.Contains("not backed by a SQL project path", exception.Message);
    }

    [Fact]
    public async Task CopyAtomicallyReplacesDestination()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dacpac copy {Guid.NewGuid():N}");
        var source = Path.Combine(directory, "source.dacpac");
        var destination = Path.Combine(directory, "output", "schema.dacpac");
        Directory.CreateDirectory(directory);

        try
        {
            await File.WriteAllTextAsync(source, "new");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await File.WriteAllTextAsync(destination, "old");

            DacpacPipelineArtifacts.Copy(source, destination);

            Assert.Equal("new", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
            Assert.DoesNotContain(
                Directory.EnumerateFiles(Path.GetDirectoryName(destination)!),
                path => path.EndsWith(".tmp", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CopyAllowsSourceAndDestinationToMatch()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dacpac same path {Guid.NewGuid():N}");
        var source = Path.Combine(directory, "schema.dacpac");
        Directory.CreateDirectory(directory);

        try
        {
            await File.WriteAllTextAsync(source, "dacpac");

            DacpacPipelineArtifacts.Copy(source, source);

            Assert.Equal("dacpac", await File.ReadAllTextAsync(source, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CopyRejectsMissingSourceEvenWhenPathsMatch()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "schema.dacpac");

        var exception = Assert.Throws<DistributedApplicationException>(
            () => DacpacPipelineArtifacts.Copy(path, path));

        Assert.Contains("was not found", exception.Message);
    }

    [Fact]
    public void CreateProfilePreservesOptionsAndReplacesSecrets()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dacpac profile {Guid.NewGuid():N}");
        var sourceProfile = Path.Combine(directory, "source.publish.xml");
        var outputProfile = Path.Combine(directory, "output.publish.xml");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(
                sourceProfile,
                """
                <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <PropertyGroup>
                    <TargetConnectionString>Server=old;Password=profile-secret</TargetConnectionString>
                    <TargetDatabaseName>OldDatabase</TargetDatabaseName>
                    <BlockOnPossibleDataLoss>True</BlockOnPossibleDataLoss>
                    <DropObjectsNotInSource>False</DropObjectsNotInSource>
                  </PropertyGroup>
                  <ItemGroup>
                    <SqlCmdVariable Include="Environment">
                      <Value>Staging</Value>
                    </SqlCmdVariable>
                  </ItemGroup>
                </Project>
                """);

            var secrets = SqlPackageRunner.CreateProfile(
                new(
                    SqlPackageAction.DeployReport,
                    "170.3.93",
                    "source.dacpac",
                    "report.xml",
                    "Server=new;Initial Catalog=NewDatabase;Password=target-secret",
                    "NewDatabase",
                    sourceProfile),
                outputProfile);
            var document = XDocument.Load(outputProfile);

            Assert.Contains("Server=old;Password=profile-secret", secrets);
            Assert.Equal(
                "Server=new;Initial Catalog=NewDatabase;Password=target-secret",
                document.Descendants().Single(element => element.Name.LocalName == "TargetConnectionString").Value);
            Assert.Equal(
                "NewDatabase",
                document.Descendants().Single(element => element.Name.LocalName == "TargetDatabaseName").Value);
            Assert.Equal(
                "True",
                document.Descendants().Single(element => element.Name.LocalName == "BlockOnPossibleDataLoss").Value);
            Assert.Equal(
                "Staging",
                document.Descendants().Single(element => element.Name.LocalName == "Value").Value);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CreateProfileRejectsDocumentTypeDefinitions()
    {
        var directory = Directory.CreateTempSubdirectory("dacpac-dtd-");
        var sourceProfile = Path.Combine(directory.FullName, "source.publish.xml");

        try
        {
            File.WriteAllText(
                sourceProfile,
                """
                <!DOCTYPE Project [<!ENTITY secret SYSTEM "file:///etc/passwd">]>
                <Project><PropertyGroup><Value>&secret;</Value></PropertyGroup></Project>
                """);

            Assert.Throws<System.Xml.XmlException>(() => SqlPackageRunner.CreateProfile(
                new(
                    SqlPackageAction.DeployReport,
                    "170.3.93",
                    "source.dacpac",
                    "report.xml",
                    "Server=new;Password=target-secret",
                    null,
                    sourceProfile),
                Path.Combine(directory.FullName, "output.publish.xml")));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("170.*")]
    [InlineData("[170.0.0,)")]
    public void SqlPackageVersionMustBeExact(string version)
    {
        var builder = CreateBuilder();
        builder.Configuration["DacpacPipeline:schema:SqlPackageVersion"] = version;

        var exception = Assert.Throws<DistributedApplicationException>(() => AddSqlProjectFromPath(builder));

        Assert.Contains("exact NuGet version", exception.Message);
    }

    [Fact]
    public void SecretRedactorRedactsFullMultilineValuesAndPasswordsOnly()
    {
        var redactor = SqlPackageSecretRedactor.Create(
        [
            "Server=host;Password=sensitive-value;Initial Catalog=database;Encrypt=True",
            "first-line\nsecond-line"
        ]);

        var value = "sensitive-value first-line second-line Connecting to host, Encrypt: True";
        var redacted = redactor.Redact(value);

        Assert.DoesNotContain("sensitive-value", redacted);
        Assert.DoesNotContain("first-line", redacted);
        Assert.DoesNotContain("second-line", redacted);
        Assert.Contains("Connecting to host, Encrypt: True", redacted);
    }

    [Fact]
    public void SecretRedactorParsesShortAndQuotedPasswords()
    {
        var redactor = SqlPackageSecretRedactor.Create(
        [
            "Server=localhost;Password=abc;Initial Catalog=db",
            "Server=localhost;Password=\"p@ss;word\";Initial Catalog=db",
            "Server=localhost;Password=\"x1\r\ny2\";Initial Catalog=db"
        ]);

        Assert.DoesNotContain("abc", redactor.Redact("password=abc"));
        Assert.DoesNotContain("p@ss;word", redactor.Redact("password=p@ss;word"));
        Assert.DoesNotContain("x1", redactor.Redact("password=x1"));
        Assert.DoesNotContain("y2", redactor.Redact("y2"));
    }

    [Fact]
    public void SqlPackageArgumentsHaveStableOrderAndDoNotExposeTarget()
    {
        foreach (var item in new[]
        {
            (Action: SqlPackageAction.DeployReport, Output: "/OutputPath:report.xml"),
            (Action: SqlPackageAction.Script, Output: "/DeployScriptPath:report.xml")
        })
        {
            var request = new SqlPackageRunRequest(
                item.Action,
                "170.3.93",
                "database.dacpac",
                "report.xml",
                "Server=server;Password=secret",
                "database",
                null);

            var arguments = SqlPackageRunner.BuildArguments(request, "profile.publish.xml");

            Assert.Equal(
                [
                    "dnx",
                    "Microsoft.SqlPackage@170.3.93",
                    "--yes",
                    "--",
                    $"/Action:{item.Action}",
                    "/SourceFile:database.dacpac",
                    "/Profile:profile.publish.xml",
                    item.Output
                ],
                arguments);
            Assert.DoesNotContain(arguments, argument => argument.Contains("secret", StringComparison.Ordinal));
            Assert.DoesNotContain(arguments, argument => argument.Contains("TargetConnectionString", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void PublishArgumentsHaveNoOutputPath()
        {
            var request = new SqlPackageRunRequest(
                SqlPackageAction.Publish,
                "170.3.93",
                "database.dacpac",
                null,
                "Server=server;Password=secret",
                "database",
                null);

            var arguments = SqlPackageRunner.BuildArguments(request, "profile.publish.xml");

            Assert.Equal(
                [
                    "dnx",
                    "Microsoft.SqlPackage@170.3.93",
                    "--yes",
                    "--",
                    "/Action:Publish",
                    "/SourceFile:database.dacpac",
                    "/Profile:profile.publish.xml"
                ],
                arguments);
        }

    [Fact]
    public void ProfileDefaultsToBlockingPossibleDataLoss()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"dacpac default profile {Guid.NewGuid():N}");
            var profile = Path.Combine(directory, "deployment.publish.xml");
            Directory.CreateDirectory(directory);

            try
            {
                SqlPackageRunner.CreateProfile(
                    new(
                        SqlPackageAction.Publish,
                        "170.3.93",
                        "database.dacpac",
                        null,
                        "Server=server;Database=database",
                        "database",
                        null),
                    profile);

                var document = XDocument.Load(profile);
                Assert.Equal(
                    bool.TrueString,
                    document.Descendants().Single(
                        element => element.Name.LocalName == "BlockOnPossibleDataLoss").Value);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void ProfilePreservesExplicitDataLossOverride()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"dacpac explicit profile {Guid.NewGuid():N}");
            var source = Path.Combine(directory, "source.publish.xml");
            var output = Path.Combine(directory, "output.publish.xml");
            Directory.CreateDirectory(directory);

            try
            {
                File.WriteAllText(
                    source,
                    """
                    <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                      <PropertyGroup>
                        <BlockOnPossibleDataLoss>False</BlockOnPossibleDataLoss>
                      </PropertyGroup>
                    </Project>
                    """);

                SqlPackageRunner.CreateProfile(
                    new(
                        SqlPackageAction.Publish,
                        "170.3.93",
                        "database.dacpac",
                        null,
                        "Server=server;Database=database",
                        "database",
                        source),
                    output);

                var document = XDocument.Load(output);
                Assert.Equal(
                    bool.FalseString,
                    document.Descendants().Single(
                        element => element.Name.LocalName == "BlockOnPossibleDataLoss").Value);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void TargetParameterOverridesReferenceAndKeepsTargetNameUnspecified()
        {
            var builder = CreateBuilder();
            builder.Configuration["ConnectionStrings:Target"] =
                "Server=localhost;Database=Target;Integrated Security=True";
            builder.AddParameter("pipeline-target", secret: true);
            var target = builder.AddConnectionString("Target");
            var resource = AddSqlProjectFromPath(builder)
                .WithPipelineOptions(new() { TargetConnectionStringParameterName = "pipeline-target" })
                .WithReference(target);
            using var app = builder.Build();
            var model = app.Services.GetRequiredService<DistributedApplicationModel>();
            var sqlProject = Assert.Single(model.Resources.OfType<SqlProjectResource>());
            Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options));

            var actionTarget = SqlProjectPipelineSteps.GetTarget(sqlProject, model, options.Options);

            var parameter = Assert.IsType<ParameterResource>(actionTarget.ConnectionString);
            Assert.Equal("pipeline-target", parameter.Name);
            Assert.Null(actionTarget.DatabaseName);
        }

        [Fact]
        public void TargetParameterMustBeSecret()
        {
            var builder = CreateBuilder();
            builder.AddParameter("pipeline-target", secret: false);
            var resource = AddSqlProjectFromPath(builder)
                .WithPipelineOptions(new()
                {
                    TargetConnectionStringParameterName = "pipeline-target"
                });
            using var app = builder.Build();
            var model = app.Services.GetRequiredService<DistributedApplicationModel>();
            var sqlProject = Assert.Single(model.Resources.OfType<SqlProjectResource>());
            Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options));

            var exception = Assert.Throws<DistributedApplicationException>(
                () => SqlProjectPipelineSteps.GetTarget(sqlProject, model, options.Options));

            Assert.Contains("must be a secret parameter", exception.Message);
        }

    [Fact]
    public void DeployStepUsesSqlPackagePublishWithoutArtifact()
        {
            var builder = CreateBuilder();
            builder.Configuration["DacpacPipeline:schema:Source"] = "File";
            builder.Configuration["ConnectionStrings:Target"] =
                "Server=localhost;Database=Target;Integrated Security=True";
            var target = builder.AddConnectionString("Target");
            var resource = AddSqlProjectFromPath(builder).WithReference(target);
            using var app = builder.Build();
            var model = app.Services.GetRequiredService<DistributedApplicationModel>();
            var sqlProject = Assert.Single(model.Resources.OfType<SqlProjectResource>());
            Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options));

            var deploy = SqlProjectPipelineSteps.CreateSteps(sqlProject, options.Options)
                .Single(step => step.Name == "deploy-schema");

            Assert.StartsWith("Deploys", deploy.Description);
            Assert.DoesNotContain(deploy.RequiredBySteps, step => step == WellKnownPipelineSteps.Deploy);
            Assert.Contains(WellKnownPipelineSteps.ProcessParameters, deploy.DependsOnSteps);
    }

    [Fact]
    public void IncludeInDeployOnlyPullsDeployIntoAggregateCommand()
    {
            var builder = CreateBuilder();
            builder.Configuration["ConnectionStrings:Target"] =
                "Server=localhost;Database=Target;Integrated Security=True";
            var target = builder.AddConnectionString("Target");
            var resource = AddSqlProjectFromPath(builder)
                .WithPipelineOptions(new() { IncludeInDeploy = true })
                .WithReference(target);
            using var app = builder.Build();
            var model = app.Services.GetRequiredService<DistributedApplicationModel>();
            var sqlProject = Assert.Single(model.Resources.OfType<SqlProjectResource>());
            Assert.Same(resource.Resource, sqlProject);
            Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options));

            var steps = SqlProjectPipelineSteps.CreateSteps(sqlProject, options.Options).ToList();

            Assert.Contains(WellKnownPipelineSteps.Deploy, steps.Single(s => s.Name == "deploy-schema").RequiredBySteps);
            Assert.DoesNotContain(steps, step =>
                step.Name != "deploy-schema"
                && step.RequiredBySteps.Contains(WellKnownPipelineSteps.Deploy));
    }

    [Fact]
    public void MultipleResourcesRegisterOneGlobalPipelineConfiguration()
    {
        var builder = CreateBuilder();
        builder.Configuration["ConnectionStrings:Target"] =
                "Server=localhost;Database=Target;Integrated Security=True";
        var target = builder.AddConnectionString("Target");

        AddSqlProjectFromPath(builder).WithReference(target);
        builder.AddSqlProject("other", Path.Combine(AppContext.BaseDirectory, "Other.sqlproj"))
                .WithReference(target);
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var configurations = model.Resources
                .OfType<SqlProjectResource>()
                .SelectMany(resource =>
                    resource.TryGetAnnotationsOfType<PipelineConfigurationAnnotation>(out var annotations)
                        ? annotations
                        : [])
                .ToList();

        Assert.Single(configurations);
    }

    [Fact]
    public void DeploySerializationUsesDatabaseTarget()
    {
        var builder = CreateBuilder();
        var targetA = builder.AddConnectionString("TargetA");
        var targetB = builder.AddConnectionString("TargetB");
        var first = AddSqlProjectFromPath(builder)
            .WithPipelineOptions(new() { IncludeInDeploy = true })
            .WithReference(targetA);
        var second = builder.AddSqlProject("schema-b", Path.Combine(AppContext.BaseDirectory, "B.sqlproj"))
            .WithPipelineOptions(new() { IncludeInDeploy = true })
            .WithReference(targetA);
        var independent = builder.AddSqlProject("schema-c", Path.Combine(AppContext.BaseDirectory, "C.sqlproj"))
            .WithPipelineOptions(new() { IncludeInDeploy = true })
            .WithReference(targetB);
        var steps = new List<PipelineStep>();
        foreach (var resource in new[] { first.Resource, second.Resource, independent.Resource })
        {
            Assert.True(resource.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options));
            steps.AddRange(SqlProjectPipelineSteps.CreateSteps(resource, options.Options));
        }

        SqlProjectPipelineSteps.WireAggregateDeploySteps(steps);

        var deployA = steps.Single(step => step.Name == "deploy-schema");
        var deployB = steps.Single(step => step.Name == "deploy-schema-b");
        var deployC = steps.Single(step => step.Name == "deploy-schema-c");
        Assert.Contains(deployA.Name, deployB.DependsOnSteps);
        Assert.DoesNotContain(deployA.Name, deployC.DependsOnSteps);
        Assert.DoesNotContain(deployB.Name, deployC.DependsOnSteps);
    }

    [Fact]
    public void DeployConfigurationIncludesNonOptedInClosureAndProvisioning()
    {
        var builder = CreateBuilder();
        var target = builder.AddConnectionString("Target");
        var prerequisite = AddSqlProjectFromPath(builder).WithReference(target);
        var dependent = builder.AddSqlProject("schema-b", Path.Combine(AppContext.BaseDirectory, "B.sqlproj"))
            .WithPipelineOptions(new() { IncludeInDeploy = true })
            .WithReference(target)
            .WaitForCompletion(prerequisite);
        var steps = new List<PipelineStep>
        {
            new()
            {
                Name = "provision-target",
                Tags = [WellKnownPipelineTags.ProvisionInfrastructure],
                Action = _ => Task.CompletedTask
            }
        };
        foreach (var resource in new[] { prerequisite.Resource, dependent.Resource })
        {
            Assert.True(resource.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options));
            steps.AddRange(SqlProjectPipelineSteps.CreateSteps(resource, options.Options));
        }

        SqlProjectPipelineSteps.WireAggregateDeploySteps(steps);

        var prerequisiteDeploy = steps.Single(step => step.Name == "deploy-schema");
        var dependentDeploy = steps.Single(step => step.Name == "deploy-schema-b");
        Assert.Contains("provision-target", prerequisiteDeploy.DependsOnSteps);
        Assert.Contains("provision-target", dependentDeploy.DependsOnSteps);
        Assert.Contains(prerequisiteDeploy.Name, dependentDeploy.DependsOnSteps);
    }

    [Fact]
    public void SerializationUsesEffectiveTargetParameter()
    {
        var builder = CreateBuilder();
        var firstTarget = builder.AddConnectionString("TargetA");
        var secondTarget = builder.AddConnectionString("TargetB");
        builder.AddParameter("pipeline-target", secret: true);
        var options = new DacpacPipelineOptions
        {
            IncludeInDeploy = true,
            TargetConnectionStringParameterName = "pipeline-target"
        };
        var first = AddSqlProjectFromPath(builder)
            .WithPipelineOptions(options)
            .WithReference(firstTarget);
        var second = builder.AddSqlProject("schema-b", Path.Combine(AppContext.BaseDirectory, "B.sqlproj"))
            .WithPipelineOptions(options)
            .WithReference(secondTarget);
        var steps = CreateDeploySteps(first.Resource, second.Resource);

        SqlProjectPipelineSteps.WireAggregateDeploySteps(steps);

        var firstDeploy = steps.Single(step => step.Name == "deploy-schema");
        var secondDeploy = steps.Single(step => step.Name == "deploy-schema-b");
        Assert.Contains(firstDeploy.Name, secondDeploy.DependsOnSteps);
    }

    [Fact]
    public void SerializationRespectsReverseAlphabeticalDependency()
    {
        var builder = CreateBuilder();
        var target = builder.AddConnectionString("Target");
        var last = builder.AddSqlProject("schema-z", Path.Combine(AppContext.BaseDirectory, "Z.sqlproj"))
            .WithPipelineOptions(new() { IncludeInDeploy = true })
            .WithReference(target);
        var first = AddSqlProjectFromPath(builder)
            .WithPipelineOptions(new() { IncludeInDeploy = true })
            .WithReference(target)
            .WaitForCompletion(last);
        var steps = CreateDeploySteps(first.Resource, last.Resource);

        SqlProjectPipelineSteps.WireAggregateDeploySteps(steps);

        var firstDeploy = steps.Single(step => step.Name == "deploy-schema");
        var lastDeploy = steps.Single(step => step.Name == "deploy-schema-z");
        Assert.Contains(lastDeploy.Name, firstDeploy.DependsOnSteps);
        Assert.DoesNotContain(firstDeploy.Name, lastDeploy.DependsOnSteps);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SerializationRejectsDependencyCycle(bool useDifferentTargets)
    {
        var builder = CreateBuilder();
        var target = builder.AddConnectionString("Target");
        var secondTarget = useDifferentTargets ? builder.AddConnectionString("Other") : target;
        var first = AddSqlProjectFromPath(builder)
            .WithPipelineOptions(new() { IncludeInDeploy = true })
            .WithReference(target);
        var second = builder.AddSqlProject("schema-b", Path.Combine(AppContext.BaseDirectory, "B.sqlproj"))
            .WithPipelineOptions(new() { IncludeInDeploy = true })
            .WithReference(secondTarget)
            .WaitForCompletion(first);
        first.WaitForCompletion(second);
        var steps = CreateDeploySteps(first.Resource, second.Resource);

        var exception = Assert.Throws<DistributedApplicationException>(
            () => SqlProjectPipelineSteps.WireAggregateDeploySteps(steps));

        Assert.Contains("contain a cycle", exception.Message);
    }

    [Fact]
    public void SerializationPreservesTransitiveDependencyThroughOtherTarget()
    {
        var builder = CreateBuilder();
        var sharedTarget = builder.AddConnectionString("Shared");
        var otherTarget = builder.AddConnectionString("Other");
        var first = AddSqlProjectFromPath(builder)
            .WithPipelineOptions(new() { IncludeInDeploy = true })
            .WithReference(sharedTarget);
        var middle = builder.AddSqlProject("schema-m", Path.Combine(AppContext.BaseDirectory, "M.sqlproj"))
            .WithReference(otherTarget)
            .WaitForCompletion(first);
        var last = builder.AddSqlProject("schema-z", Path.Combine(AppContext.BaseDirectory, "Z.sqlproj"))
            .WithPipelineOptions(new() { IncludeInDeploy = true })
            .WithReference(sharedTarget)
            .WaitForCompletion(middle);
        var steps = CreateDeploySteps(first.Resource, middle.Resource, last.Resource);

        SqlProjectPipelineSteps.WireAggregateDeploySteps(steps);

        var firstDeploy = steps.Single(step => step.Name == "deploy-schema");
        var lastDeploy = steps.Single(step => step.Name == "deploy-schema-z");
        Assert.Contains(firstDeploy.Name, lastDeploy.DependsOnSteps);
    }

    [Fact]
    public async Task LocalSqlServerReferenceUsesSecretPipelineParameter()
    {
        var builder = CreateBuilder();
        var database = builder.AddSqlServer("sql").AddDatabase("Database");
        var resource = AddSqlProjectFromPath(builder).WithReference(database);
        using var app = builder.Build();
        await ConfigurePipelineAsync(builder, app);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var sqlProject = Assert.Single(model.Resources.OfType<SqlProjectResource>());
        Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options));
        Assert.True(sqlProject.TryGetLastAnnotation<DacpacTargetAnnotation>(out var target));
        Assert.NotNull(target.PipelineConnectionStringParameter);

        var resolved = SqlProjectPipelineSteps.GetTarget(sqlProject, model, options.Options);

        Assert.Same(target.PipelineConnectionStringParameter, resolved.ConnectionString);
        Assert.True(target.PipelineConnectionStringParameter.Secret);
    }

    [Fact]
    public async Task LocalSqlServerReferencesShareOnePipelineParameter()
    {
        var builder = CreateBuilder();
        var database = builder.AddSqlServer("sql").AddDatabase("Database");
        AddSqlProjectFromPath(builder).WithReference(database);
        builder.AddSqlProject("schema-b", Path.Combine(AppContext.BaseDirectory, "B.sqlproj"))
            .WithReference(database);
        using var app = builder.Build();
        await ConfigurePipelineAsync(builder, app);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var parameter = Assert.Single(
            model.Resources.OfType<ParameterResource>(),
            parameter => parameter.Name == "Database-connection-string");
        var sqlProjects = model.Resources.OfType<SqlProjectResource>().ToList();
        Assert.All(sqlProjects, resource =>
        {
            Assert.True(resource.TryGetLastAnnotation<DacpacTargetAnnotation>(out var target));
            Assert.Same(parameter, target.PipelineConnectionStringParameter);
        });
    }

    [Fact]
    public async Task LocalSqlServerReferenceRejectsExistingNonSecretParameter()
    {
        var builder = CreateBuilder();
        builder.AddParameter("Database-connection-string", secret: false);
        var database = builder.AddSqlServer("sql").AddDatabase("Database");

        AddSqlProjectFromPath(builder).WithReference(database);
        using var app = builder.Build();
        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(
            () => ConfigurePipelineAsync(builder, app));

        Assert.Contains("must be a secret parameter", exception.Message);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExplicitTargetDoesNotRegisterOrValidateLocalFallback(
        bool configureTargetFirst, bool hasNonSecretFallback)
    {
        var builder = CreateBuilder();
        var database = builder.AddSqlServer("sql").AddDatabase("Database");
        var target = builder.AddParameter("explicit-target", secret: true).Resource;
        var fallback = hasNonSecretFallback
            ? builder.AddParameter("Database-connection-string").Resource
            : null;
        var project = AddSqlProjectFromPath(builder);
        var options = new DacpacPipelineOptions { TargetConnectionStringParameterName = target.Name };
        if (configureTargetFirst)
        {
            project.WithPipelineOptions(options).WithReference(database);
        }
        else
        {
            project.WithReference(database).WithPipelineOptions(options);
        }

        using var app = builder.Build();
        await ConfigurePipelineAsync(builder, app);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.Same(fallback, model.Resources.OfType<ParameterResource>()
            .SingleOrDefault(parameter => parameter.Name == "Database-connection-string"));
        Assert.True(project.Resource.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var effectiveOptions));
        Assert.Same(target, SqlProjectPipelineSteps.GetTarget(project.Resource, model, effectiveOptions.Options).ConnectionString);
    }

    [Fact]
    public async Task TargetLockSerializesAliasedConnectionStrings()
    {
        var targetLock = new SqlPackageTargetLock();
        var first = await targetLock.AcquireAsync(
            "Server=server;Initial Catalog=database;Password=one",
            null,
            TestContext.Current.CancellationToken);
        var secondTask = targetLock.AcquireAsync(
            "Data Source=SERVER;Database=DATABASE;Password=two",
            null,
            TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(secondTask.IsCompleted);
        first.Dispose();
        using var second = await secondTask;
    }

    [Fact]
    public void ExistingMetadataUsesAssemblyConfiguration()
    {
        var builder = CreateBuilder();
        builder.AddSqlProject<TestProject>("schema");
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<SqlProjectResource>());
        Assert.True(resource.TryGetLastAnnotation<IProjectMetadata>(out var metadata));

        var configuration = SqlProjectDacpacResolver.GetOutputConfiguration(
            resource,
            metadata,
            null);

        Assert.Equal(
            typeof(TestProject).Assembly
                .GetCustomAttribute<AssemblyConfigurationAttribute>()?
                .Configuration,
            configuration);
    }

    [Fact]
    public void BuildAndPublishWithoutFileSourceDoNotRegisterDacpacPathParameter()
    {
        var builder = CreateBuilder();
        var resource = AddSqlProjectFromPath(builder);
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var sqlProject = Assert.Single(model.Resources.OfType<SqlProjectResource>());
        Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options));

        var steps = SqlProjectPipelineSteps.CreateSteps(sqlProject, options.Options).ToList();

        Assert.DoesNotContain(model.Resources.OfType<ParameterResource>(), parameter =>
            parameter.Name == "schema-dacpac-path");
        Assert.Contains(steps, step => step.Name == "build-schema");
        Assert.Equal(["build-schema"], steps.Single(step => step.Name == "publish-schema").DependsOnSteps);
        Assert.DoesNotContain(
            WellKnownPipelineSteps.ProcessParameters,
            steps.Single(step => step.Name == "publish-schema").DependsOnSteps);
    }

    [Theory]
    [InlineData(DacpacSource.Build)]
    [InlineData(DacpacSource.Existing)]
    public void SwitchingAwayFromFileSourceRemovesDacpacPathParameter(DacpacSource finalSource)
    {
        var builder = CreateBuilder();
        AddSqlProjectFromPath(builder)
            .WithPipelineOptions(new() { Source = DacpacSource.File })
            .WithPipelineOptions(new() { Source = finalSource });

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var sqlProject = Assert.Single(model.Resources.OfType<SqlProjectResource>());

        Assert.DoesNotContain(model.Resources.OfType<ParameterResource>(), parameter =>
            parameter.Name == "schema-dacpac-path");
        Assert.False(sqlProject.HasAnnotationOfType<DacpacPipelinePathParameterAnnotation>());
    }

    [Theory]
    [InlineData(false, false, null, true)]
    [InlineData(true, false, null, true)]
    [InlineData(false, true, null, true)]
    [InlineData(true, true, null, true)]
    [InlineData(false, false, DacpacSource.Existing, false)]
    [InlineData(true, true, DacpacSource.Existing, false)]
    public void IncludeFlagsDoNotChooseWhetherToBuild(
        bool includeInPublish, bool includeInDeploy, DacpacSource? source, bool expectBuild)
    {
        var builder = CreateBuilder();
        AddSqlProjectFromPath(builder).WithPipelineOptions(new()
        {
            IncludeInPublish = includeInPublish,
            IncludeInDeploy = includeInDeploy,
            Source = source
        });
        using var app = builder.Build();
        var sqlProject = Assert.Single(
            app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<SqlProjectResource>());
        Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options));

        var steps = SqlProjectPipelineSteps.CreateSteps(sqlProject, options.Options).ToList();

        Assert.All(
            steps.Where(step => step.Name is "publish-schema" or "report-schema" or "script-schema" or "deploy-schema"),
            step => Assert.Equal(
                expectBuild,
                step.DependsOnSteps.Contains("build-schema")));
    }

    [Fact]
    public async Task ExistingSourceEvaluatesOutputWithoutBuilding()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dacpac existing {Guid.NewGuid():N}");
        var projectPath = Path.Combine(directory, "Database.sqlproj");
        var dacpacPath = Path.Combine(directory, "bin", "Debug", "Database.dacpac");
        Directory.CreateDirectory(Path.GetDirectoryName(dacpacPath)!);

        try
        {
            File.WriteAllText(
                projectPath,
                """
                <Project>
                  <PropertyGroup>
                    <TargetPath>bin/Debug/Database.dacpac</TargetPath>
                  </PropertyGroup>
                  <Target Name="Build">
                    <Error Text="Build must not run for Existing source." />
                  </Target>
                </Project>
                """);
            File.WriteAllText(dacpacPath, "existing dacpac");
            var builder = CreateBuilder();
            var resource = builder.AddSqlProject("schema", projectPath, "Debug");
            using var app = builder.Build();
            var resolver = app.Services.GetRequiredService<SqlProjectDacpacResolver>();

            using var resolved = await resolver.ResolveExistingAsync(
                resource.Resource,
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                TestContext.Current.CancellationToken);

            Assert.Equal(Path.GetFullPath(dacpacPath), Path.GetFullPath(resolved.Path));
            Assert.Equal("existing dacpac", File.ReadAllText(resolved.Path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TemporaryProfileDirectoryIsPrivate()
    {
        var directory = SqlPackageRunner.CreatePrivateProfileDirectory();
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var security = System.IO.FileSystemAclExtensions.GetAccessControl(directory);
                Assert.True(security.AreAccessRulesProtected);
                var rules = security.GetAccessRules(
                    includeExplicit: true, includeInherited: true, typeof(System.Security.Principal.SecurityIdentifier));
                var rule = Assert.Single(rules.Cast<System.Security.AccessControl.FileSystemAccessRule>());
                Assert.Equal(identity.User, rule.IdentityReference);
                Assert.True(rule.InheritanceFlags.HasFlag(System.Security.AccessControl.InheritanceFlags.ObjectInherit));
            }
            else
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                    File.GetUnixFileMode(directory.FullName));
            }
        }
        finally
        {
            directory.Delete();
        }
    }

    [Fact]
    public void WithReferenceAddsExplicitPipelineTarget()
    {
        var builder = CreateBuilder();
        builder.Configuration["ConnectionStrings:Target"] =
            "Server=localhost;Database=Target;Integrated Security=True";
        var target = builder.AddConnectionString("Target");

        var resource = AddSqlProjectFromPath(builder).WithReference(target);

        Assert.True(resource.Resource.TryGetLastAnnotation<DacpacTargetAnnotation>(out var annotation));
        Assert.Same(target.Resource, annotation.Target);
        Assert.Null(annotation.TargetDatabaseName);
    }

    [Fact]
    public async Task RunnerRedactsOutputAndExceptionAfterNonZeroExit()
    {
        var secret = "highly-sensitive-password";
        var logger = new RecordingLogger();
        var startInfo = CreateShellStartInfo(
            $"Write-Output 'prefix {secret} suffix'; [Console]::Error.WriteLine('stderr {secret}'); exit 7",
            $"echo 'prefix {secret} suffix'; echo 'stderr {secret}' >&2; exit 7");

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            SqlPackageRunner.RunProcessAsync(
                startInfo,
                SqlPackageSecretRedactor.Create([secret]),
                SqlPackageAction.Publish,
                logger,
                TestContext.Current.CancellationToken));

        Assert.Contains("exit code 7", exception.Message);
        Assert.DoesNotContain(secret, exception.Message);
        Assert.DoesNotContain(secret, string.Join(Environment.NewLine, logger.Messages));
        Assert.Contains("[REDACTED]", string.Join(Environment.NewLine, logger.Messages));
    }

    [Fact]
    public async Task RunnerCancellationTerminatesPromptly()
    {
        var logger = new RecordingLogger();
        var startInfo = CreateShellStartInfo(
            "Start-Sleep -Seconds 30",
            "sleep 30");
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(200));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            SqlPackageRunner.RunProcessAsync(
                startInfo,
                SqlPackageSecretRedactor.Create([]),
                SqlPackageAction.Publish,
                logger,
                cancellation.Token));

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Cancellation took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void PipelineRejectsNonSerializableDeployCallback()
    {
        var builder = CreateBuilder();
        builder.Configuration["ConnectionStrings:Target"] =
            "Server=localhost;Database=Target;Integrated Security=True";
        var target = builder.AddConnectionString("Target");
        var resource = AddSqlProjectFromPath(builder)
            .WithConfigureDacDeployOptions(options => options.BlockOnPossibleDataLoss = false)
            .WithReference(target);
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var sqlProject = Assert.Single(model.Resources.OfType<SqlProjectResource>());
        Assert.True(sqlProject.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options));
        var exception = Assert.Throws<DistributedApplicationException>(
            () => SqlProjectPipelineSteps.GetProfilePath(sqlProject, app.Services));

        Assert.Contains("WithConfigureDacDeployOptions", exception.Message);
    }

    [Fact]
    public void RunModeDoesNotRegisterPipelineArtifactsOrParameters()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["DacpacPipeline:schema:Source"] = "File";

        var resource = AddSqlProjectFromPath(builder).WithPipelineOptions();

        Assert.False(resource.Resource.HasAnnotationOfType<DacpacPipelineRegistrationAnnotation>());
        Assert.DoesNotContain(builder.Resources, candidate => candidate.Name == "schema-dacpac-path");
    }

    private static IResourceBuilder<SqlProjectResource> AddSqlProjectFromPath(IDistributedApplicationBuilder builder)
        => builder.AddSqlProject(
            "schema",
            Path.Combine(AppContext.BaseDirectory, "Schema", "Schema.sqlproj"),
            configuration: "Debug");

    private static IDistributedApplicationBuilder CreateBuilder()
        => TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

    private static Task ConfigurePipelineAsync(IDistributedApplicationBuilder builder, DistributedApplication app)
    {
        var pipeline = app.Services.GetRequiredService<IDistributedApplicationPipeline>();
        pipeline.AddStep("inspect-dacpac-model", _ => Task.CompletedTask);
        app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PipelineOptions>>().Value.Step = "inspect-dacpac-model";
        return pipeline.ExecuteAsync(new PipelineContext(
            app.Services.GetRequiredService<DistributedApplicationModel>(),
            builder.ExecutionContext,
            app.Services,
            NullLogger.Instance,
            TestContext.Current.CancellationToken));
    }

    private static List<PipelineStep> CreateDeploySteps(params IResourceWithDacpac[] resources)
    {
        var steps = new List<PipelineStep>();
        foreach (var resource in resources)
        {
            Assert.True(resource.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options));
            steps.AddRange(SqlProjectPipelineSteps.CreateSteps(resource, options.Options));
        }

        return steps;
    }

    private static ProcessStartInfo CreateShellStartInfo(string windowsScript, string unixScript)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(OperatingSystem.IsWindows() ? "-NoProfile" : "-c");
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("-Command");
        }

        startInfo.ArgumentList.Add(OperatingSystem.IsWindows() ? windowsScript : unixScript);
        return startInfo;
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}

#pragma warning restore CTASPIREPIPELINES001
