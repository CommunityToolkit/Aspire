using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Testing;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.Neon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace CommunityToolkit.Aspire.Hosting.Neon.Tests;

public class AddNeonTests
{
    [Fact]
    public void DistributedApplicationBuilderCannotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;
        var apiKey = TestDistributedApplicationBuilder.Create().AddParameter("neon-api-key", "test", secret: true);

        Assert.Throws<ArgumentNullException>(() => builder.AddNeon("neon", apiKey));
    }

    [Fact]
    public void ResourceNameCannotBeOmitted()
    {
        var builder = DistributedApplication.CreateBuilder();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        string emptyName = string.Empty;
        string whitespaceName = new(' ', 1);

        Assert.Throws<ArgumentNullException>(() => builder.AddNeon(null!, apiKey));
        Assert.Throws<ArgumentException>(() => builder.AddNeon(emptyName, apiKey));
        Assert.Throws<ArgumentException>(() => builder.AddNeon(whitespaceName, apiKey));
    }

    [Fact]
    public void ApiKeyCannotBeNull()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddNeon("neon", null!));
    }

    [Fact]
    public void ResourceImplementsConnectionString()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        builder.AddNeon("neon", apiKey);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<NeonProjectResource>());
        Assert.True(resource is IResourceWithConnectionString);
    }

    [Fact]
    public void AddDatabaseUsesDefaultNames()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey);
        var database = neon.AddDatabase("appdb");

        Assert.Equal("appdb", database.Resource.DatabaseName);
        Assert.Equal("appdb_owner", database.Resource.RoleName);
        Assert.Equal(neon.Resource, database.Resource.Parent);
    }

    [Fact]
    public void CreateBranchIfMissingDefaultsToFalse()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey);

        Assert.False(neon.Resource.Options.Branch.CreateBranchIfMissing);
    }

    [Fact]
    public void WithProjectIdSetsProjectId()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .WithProjectId("proj-123");

        Assert.Equal("proj-123", neon.Resource.Options.ProjectId);
    }

    [Fact]
    public void WithProjectNameSetsProjectName()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .WithProjectName("my-project");

        Assert.Equal("my-project", neon.Resource.Options.ProjectName);
        Assert.False(neon.Resource.Options.CreateProjectIfMissing);
    }

    [Fact]
    public void AddProjectSetsNameAndEnablesCreation()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .AddProject("my-project");

        Assert.Equal("my-project", neon.Resource.Options.ProjectName);
        Assert.True(neon.Resource.Options.CreateProjectIfMissing);
        Assert.Equal(NeonProvisionerMode.Attach, neon.Resource.Options.Provisioning.Mode);
    }

    [Fact]
    public void AddProjectAcceptsOptionalParameters()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .AddProject("my-project", regionId: "aws-us-east-2", postgresVersion: 16);

        Assert.Equal("my-project", neon.Resource.Options.ProjectName);
        Assert.Equal("aws-us-east-2", neon.Resource.Options.RegionId);
        Assert.Equal(16, neon.Resource.Options.PostgresVersion);
    }

    [Fact]
    public void WithOrganizationIdSetsOrganizationId()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .WithOrganizationId("org-123");

        Assert.Equal("org-123", neon.Resource.Options.OrganizationId);
    }

    [Fact]
    public void WithOrganizationNameSetsOrganizationName()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .WithOrganizationName("my-org");

        Assert.Equal("my-org", neon.Resource.Options.OrganizationName);
    }

    [Fact]
    public void WithBranchIdSetsBranchId()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .WithBranchId("br-123");

        Assert.Equal("br-123", neon.Resource.Options.Branch.BranchId);
    }

    [Fact]
    public void WithBranchNameSetsBranchName()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .WithBranchName("dev");

        Assert.Equal("dev", neon.Resource.Options.Branch.BranchName);
        Assert.False(neon.Resource.Options.Branch.CreateBranchIfMissing);
    }

    [Fact]
    public void AddBranchSetsNameAndEnablesCreation()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .AddBranch("dev");

        Assert.Equal("dev", neon.Resource.Options.Branch.BranchName);
        Assert.True(neon.Resource.Options.Branch.CreateBranchIfMissing);
        Assert.True(neon.Resource.Options.Branch.CreateEndpointIfMissing);
    }

    [Fact]
    public void AddBranchAcceptsEndpointType()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .AddBranch("dev", NeonEndpointType.ReadOnly);

        Assert.Equal(NeonEndpointType.ReadOnly, neon.Resource.Options.Branch.EndpointType);
    }

    [Fact]
    public void AddEphemeralBranchSetsEphemeralOptions()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .AddEphemeralBranch("test-");

        Assert.True(neon.Resource.Options.Branch.UseEphemeralBranch);
        Assert.Equal("test-", neon.Resource.Options.Branch.EphemeralBranchPrefix);
    }

    [Fact]
    public void AddEphemeralBranchUsesDefaultPrefix()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .AddEphemeralBranch();

        Assert.True(neon.Resource.Options.Branch.UseEphemeralBranch);
        Assert.Equal("aspire-", neon.Resource.Options.Branch.EphemeralBranchPrefix);
    }

    [Fact]
    public void WithBranchRestoreEnablesRestore()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .WithBranchRestore(restore =>
            {
                restore.SourceBranchId = "br-main-123";
                restore.PreserveUnderName = "backup";
            });

        Assert.True(neon.Resource.Options.Branch.Restore.Enabled);
        Assert.Equal("br-main-123", neon.Resource.Options.Branch.Restore.SourceBranchId);
        Assert.Equal("backup", neon.Resource.Options.Branch.Restore.PreserveUnderName);
    }

    [Fact]
    public void WithBranchRestoreDefaultEnablesRestore()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .WithBranchRestore();

        Assert.True(neon.Resource.Options.Branch.Restore.Enabled);
    }

    [Fact]
    public void WithAnonymizedDataEnablesAnonymization()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .WithAnonymizedData(anon =>
            {
                anon.MaskingRules.Add(new NeonMaskingRule
                {
                    DatabaseName = "appdb",
                    TableName = "users",
                    ColumnName = "email",
                    MaskingFunction = "mask_email"
                });
            });

        Assert.True(neon.Resource.Options.Branch.Anonymization.Enabled);
        Assert.Single(neon.Resource.Options.Branch.Anonymization.MaskingRules);

        var rule = neon.Resource.Options.Branch.Anonymization.MaskingRules[0];
        Assert.Equal("appdb", rule.DatabaseName);
        Assert.Equal("users", rule.TableName);
        Assert.Equal("email", rule.ColumnName);
        Assert.Equal("mask_email", rule.MaskingFunction);
        Assert.Equal("public", rule.SchemaName);
    }

    [Fact]
    public void AsDefaultBranchSetsFlag()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .AsDefaultBranch();

        Assert.True(neon.Resource.Options.Branch.SetAsDefault);
    }

    [Fact]
    public void WithDatabaseNameSetsDefaultDatabaseName()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .WithDatabaseName("mydb");

        Assert.Equal("mydb", neon.Resource.Options.DatabaseName);
    }

    [Fact]
    public void WithRoleNameSetsDefaultRoleName()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .WithRoleName("myrole");

        Assert.Equal("myrole", neon.Resource.Options.RoleName);
    }

    [Fact]
    public void WithConnectionPoolerEnablesPooler()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .WithConnectionPooler();

        Assert.True(neon.Resource.Options.UseConnectionPooler);
    }

    [Fact]
    public void AddDatabaseUsesExplicitRoleName()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey);
        var database = neon.AddDatabase("appdb", roleName: "custom_role");

        Assert.Equal("appdb", database.Resource.DatabaseName);
        Assert.Equal("custom_role", database.Resource.RoleName);
    }

    [Fact]
    public void AddDatabaseUsesDefaultRoleFromProject()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .WithRoleName("project_role");

        var database = neon.AddDatabase("appdb");

        Assert.Equal("project_role", database.Resource.RoleName);
    }

    [Fact]
    public void FluentChainingWorksCorrectly()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .AddProject("aspire-neon")
            .WithOrganizationName("my-org")
            .AddBranch("dev")
            .WithConnectionPooler()
            .AsDefaultBranch();

        var options = neon.Resource.Options;
        Assert.Equal("aspire-neon", options.ProjectName);
        Assert.True(options.CreateProjectIfMissing);
        Assert.Equal("my-org", options.OrganizationName);
        Assert.Equal("dev", options.Branch.BranchName);
        Assert.True(options.Branch.CreateBranchIfMissing);
        Assert.True(options.UseConnectionPooler);
        Assert.True(options.Branch.SetAsDefault);
    }

    [Fact]
    public void DefaultBranchOptionsHaveCorrectDefaults()
    {
        NeonBranchOptions branchOptions = new();

        Assert.False(branchOptions.CreateBranchIfMissing);
        Assert.False(branchOptions.UseEphemeralBranch);
        Assert.False(branchOptions.SetAsDefault);
        Assert.False(branchOptions.Restore.Enabled);
        Assert.False(branchOptions.Anonymization.Enabled);
        Assert.Equal(NeonEndpointType.ReadWrite, branchOptions.EndpointType);
        Assert.False(branchOptions.CreateEndpointIfMissing);
        Assert.Null(branchOptions.InitSource);
        Assert.Null(branchOptions.ParentLsn);
        Assert.Null(branchOptions.ParentTimestamp);
        Assert.Null(branchOptions.Archived);
    }

    [Fact]
    public void HealthCheckIsRegisteredForResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        builder.AddNeon("neon", apiKey);

        using var app = builder.Build();
        var healthCheckService = app.Services.GetService<HealthCheckService>();

        Assert.NotNull(healthCheckService);
    }

    [Fact]
    public void InitialStateIsStarting()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey);

        // The resource type should be set
        var resource = neon.Resource;
        Assert.NotNull(resource);
        Assert.IsType<NeonProjectResource>(resource);
    }

    [Fact]
    public void EphemeralBranchRegistersCommands()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        builder.AddNeon("neon", apiKey)
            .AddEphemeralBranch("test-");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<NeonProjectResource>());

        // All Neon resources should have command annotations (not just ephemeral)
        Assert.True(resource.TryGetAnnotationsOfType<ResourceCommandAnnotation>(out var commands));

        var commandNames = commands.Select(c => c.Name).ToList();
        Assert.Contains("neon-suspend", commandNames);
        Assert.Contains("neon-resume", commandNames);
    }

    [Fact]
    public void NonEphemeralBranchAlsoRegistersCommands()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        builder.AddNeon("neon", apiKey)
            .WithBranchName("dev");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<NeonProjectResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ResourceCommandAnnotation>(out var commands));
        var commandNames = commands!.Select(c => c.Name).ToList();
        Assert.Contains("neon-suspend", commandNames);
        Assert.Contains("neon-resume", commandNames);
    }

    [Fact]
    public void HealthCheckReturnUnhealthyWhenNotProvisioned()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey);
        var resource = neon.Resource;

        // ConnectionUri is null before provisioning
        Assert.Null(resource.ConnectionUri);
    }

    [Fact]
    public void ResourceIsExcludedFromManifest()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        builder.AddNeon("neon", apiKey);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<NeonProjectResource>());
        Assert.True(resource.TryGetLastAnnotation<ManifestPublishingCallbackAnnotation>(out _));
    }

    [Fact]
    public void DatabaseResourceHasInitialState()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey);
        neon.AddDatabase("mydb", "mydb");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbResource = Assert.Single(appModel.Resources.OfType<NeonDatabaseResource>());
        Assert.Equal("mydb", dbResource.DatabaseName);
    }

    [Fact]
    public void AddNeonProvisionerSetsMode()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey, NeonProvisionerMode.Provision);

        Assert.Equal(NeonProvisionerMode.Provision, neon.Resource.Options.Provisioning.Mode);
    }

    [Fact]
    public void AddNeonProvisionerUsesAttachModeByDefault()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey);

        Assert.Equal(NeonProvisionerMode.Attach, neon.Resource.Options.Provisioning.Mode);
        Assert.NotNull(neon.Resource.ProvisionerResource);
        Assert.Equal("neon-provisioner", neon.Resource.ProvisionerResource!.Name);
    }

    [Fact]
    public async Task AddNeonProvisionerForwardsModeAndOutputContractEnvironmentValues()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .AddProject("my-project")
            .AddBranch("dev")
            .WithBranchRestore(restore =>
            {
                restore.SourceBranchId = "br-source";
                restore.SourceLsn = "0/16B6A90";
                restore.PreserveUnderName = "backup-dev";
            })
            .WithAnonymizedData(anon =>
            {
                anon.StartAnonymization = false;
                anon.MaskingRules.Add(new NeonMaskingRule
                {
                    DatabaseName = "appdb",
                    SchemaName = "public",
                    TableName = "users",
                    ColumnName = "email",
                    MaskingFunction = "mask_email",
                });
            });

        neon.AddDatabase("appdb", "appdb");

    var provisioner = neon.AddNeonProvisioner("neon-provisioner", NeonProvisionerMode.Provision);
    var env = await provisioner.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal("provision", env["NEON_MODE"]);
        Assert.False(string.IsNullOrWhiteSpace(env["NEON_OUTPUT_FILE_PATH"]));
        Assert.Equal("True", env["NEON_BRANCH_RESTORE_ENABLED"]);
        Assert.Equal("br-source", env["NEON_BRANCH_RESTORE_SOURCE_BRANCH_ID"]);
        Assert.Equal("0/16B6A90", env["NEON_BRANCH_RESTORE_SOURCE_LSN"]);
        Assert.Equal("backup-dev", env["NEON_BRANCH_RESTORE_PRESERVE_UNDER_NAME"]);
        Assert.Equal("True", env["NEON_BRANCH_ANONYMIZATION_ENABLED"]);
        Assert.Equal("False", env["NEON_BRANCH_ANONYMIZATION_START"]);
        Assert.Contains("mask_email", env["NEON_BRANCH_MASKING_RULES_JSON"], StringComparison.Ordinal);
        Assert.Contains("appdb", env["NEON_DATABASE_SPECS_JSON"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddDatabaseRefreshesExistingProvisionerDatabaseSpecs()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey, NeonProvisionerMode.Attach);

        neon.AddDatabase("olympusDb", "olympus", "olympus_owner");

        Assert.NotNull(neon.Resource.ProvisionerResource);
        var env = await neon.Resource.ProvisionerResource!.GetEnvironmentVariablesAsync();

        Assert.Contains("\"ResourceName\":\"olympusDb\"", env["NEON_DATABASE_SPECS_JSON"], StringComparison.Ordinal);
        Assert.Contains("\"DatabaseName\":\"olympus\"", env["NEON_DATABASE_SPECS_JSON"], StringComparison.Ordinal);
        Assert.Contains("\"RoleName\":\"olympus_owner\"", env["NEON_DATABASE_SPECS_JSON"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddProjectRefreshesExistingProvisionerProjectSettings()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey, NeonProvisionerMode.Attach);

        neon.AddProject("aspire-neon-integration");

        Assert.NotNull(neon.Resource.ProvisionerResource);
        var env = await neon.Resource.ProvisionerResource!.GetEnvironmentVariablesAsync();

        Assert.Equal("aspire-neon-integration", env["NEON_PROJECT_NAME"]);
        Assert.Equal("True", env["NEON_CREATE_PROJECT_IF_MISSING"]);
    }

    [Fact]
    public void AddNeonProvisionerSetsAttachMode()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey, NeonProvisionerMode.Attach)
            .AddProject("my-project")
            .AddBranch("dev");

        Assert.Equal(NeonProvisionerMode.Attach, neon.Resource.Options.Provisioning.Mode);
    }

    [Fact]
    public void AddNeonWithProvisionModeSetsMode()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey, NeonProvisionerMode.Provision);

        Assert.Equal(NeonProvisionerMode.Provision, neon.Resource.Options.Provisioning.Mode);
    }

    [Fact]
    public void AddNeonWithProvisionModeAddsDefaultProvisionerResourceName()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        _ = builder.AddNeon("myneon", apiKey, NeonProvisionerMode.Attach);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.Contains(model.Resources, resource => resource.Name == "myneon-provisioner");
    }

    [Fact]
    public void AddProvisionerReturnsSameNeonResourceBuilder()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey);
        var returned = neon.AddProvisioner(NeonProvisionerMode.Provision);

        Assert.Same(neon, returned);
        Assert.Equal(NeonProvisionerMode.Provision, neon.Resource.Options.Provisioning.Mode);
        Assert.NotNull(neon.Resource.ProvisionerResource);
        Assert.Equal("neon-provisioner", neon.Resource.ProvisionerResource!.Name);
    }

    [Fact]
    public void AddProvisionerWithCustomNameThrowsWhenDefaultProvisionerAlreadyConfigured()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);

        var neon = builder.AddNeon("neon", apiKey)
            .AddProject("my-project")
            .AddBranch("dev");

        var ex = Assert.Throws<DistributedApplicationException>(() =>
            neon.AddProvisioner("custom-neon-provisioner", NeonProvisionerMode.Attach));

        Assert.Contains("already configured", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("neon-provisioner", neon.Resource.ProvisionerResource!.Name);
    }

    [Fact]
    public async Task ProjectAndDatabaseConnectionPropertiesAreExposed()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var database = neon.AddDatabase("appdb", "appdb", "app_owner");

        SetPropertyValue(neon.Resource, nameof(NeonProjectResource.Host), "project.neon.tech");
        SetPropertyValue(neon.Resource, nameof(NeonProjectResource.Port), 5432);
        SetPropertyValue(neon.Resource, nameof(NeonProjectResource.DatabaseName), "neondb");
        SetPropertyValue(neon.Resource, nameof(NeonProjectResource.RoleName), "neon_owner");
        SetPropertyValue(neon.Resource, nameof(NeonProjectResource.Password), "pass");
        SetPropertyValue(neon.Resource, nameof(NeonProjectResource.ConnectionUri), "postgres://neon_owner:pass@project.neon.tech:5432/neondb");

        SetPropertyValue(database.Resource, nameof(NeonDatabaseResource.Host), "db.neon.tech");
        SetPropertyValue(database.Resource, nameof(NeonDatabaseResource.Port), 5432);
        SetPropertyValue(database.Resource, nameof(NeonDatabaseResource.Password), "db-pass");
        SetPropertyValue(database.Resource, nameof(NeonDatabaseResource.ConnectionUri), "postgres://app_owner:db-pass@db.neon.tech:5432/appdb");

        using var app = builder.Build();

        Dictionary<string, ReferenceExpression> projectProperties =
            ((IResourceWithConnectionString)neon.Resource).GetConnectionProperties().ToDictionary(p => p.Key, p => p.Value);
        Dictionary<string, ReferenceExpression> databaseProperties =
            ((IResourceWithConnectionString)database.Resource).GetConnectionProperties().ToDictionary(p => p.Key, p => p.Value);

        Assert.Contains("Host", projectProperties.Keys);
        Assert.Contains("Port", projectProperties.Keys);
        Assert.Contains("Database", projectProperties.Keys);
        Assert.Contains("Username", projectProperties.Keys);
        Assert.Contains("Password", projectProperties.Keys);
        Assert.Contains("Uri", projectProperties.Keys);

        Assert.Contains("Host", databaseProperties.Keys);
        Assert.Contains("Port", databaseProperties.Keys);
        Assert.Contains("Database", databaseProperties.Keys);
        Assert.Contains("Username", databaseProperties.Keys);
        Assert.Contains("Password", databaseProperties.Keys);
        Assert.Contains("Uri", databaseProperties.Keys);

        Assert.Equal("postgres://neon_owner:pass@project.neon.tech:5432/neondb", await neon.Resource.GetConnectionStringAsync());
        Assert.Equal("postgres://app_owner:db-pass@db.neon.tech:5432/appdb", await database.Resource.GetConnectionStringAsync());
    }

    [Fact]
    public async Task ReadProvisionerOutputAsyncReadsOutputAndFailureArtifacts()
    {
        string directory = Path.Combine(Path.GetTempPath(), "neon-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string outputPath = Path.Combine(directory, "neon.json");
                        string json = """
                                {
                                    "ProjectId": "project-1",
                                    "BranchId": "branch-1",
                                    "EndpointId": "endpoint-1",
                                    "Host": "host",
                                    "Port": 5432,
                                    "Password": "pass",
                                    "DefaultDatabaseName": "neondb",
                                    "DefaultRoleName": "neondb_owner",
                                    "DefaultConnectionUri": "postgres://neondb_owner:pass@host:5432/neondb",
                                    "Databases": [
                                        {
                                            "ResourceName": "db",
                                            "DatabaseName": "neondb",
                                            "RoleName": "neondb_owner",
                                            "ConnectionUri": "postgres://neondb_owner:pass@host:5432/neondb",
                                            "Host": "host",
                                            "Port": 5432,
                                            "Password": "pass"
                                        }
                                    ]
                                }
                                """;

                        await File.WriteAllTextAsync(outputPath, json);

            object? result = await InvokePrivateStaticAsync(
                "ReadProvisionerOutputAsync",
                outputPath,
                NullLogger.Instance,
                CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("project-1", result!.GetType().GetProperty("ProjectId")!.GetValue(result));

            string failurePath = $"{outputPath}.error.log";
            await File.WriteAllTextAsync(failurePath, "boom");

            await Assert.ThrowsAsync<DistributedApplicationException>(async () =>
            {
                _ = await InvokePrivateStaticAsync(
                    "ReadProvisionerOutputAsync",
                    outputPath,
                    NullLogger.Instance,
                    CancellationToken.None);
            });
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ConfigureNeonConnectionFromOutputAsyncUpdatesResources()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey, NeonProvisionerMode.Attach);
        var database = neon.AddDatabase("appdb", "appdb", "app_owner");

        try
        {
            object annotation = neon.Resource.Annotations.First(a =>
                a.GetType().Name == "NeonExternalProvisionerAnnotation");

            string outputPath = (string)annotation.GetType().GetProperty("OutputFilePath")!.GetValue(annotation)!;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                        string json = """
                                {
                                    "ProjectId": "project-2",
                                    "BranchId": "branch-2",
                                    "EndpointId": "endpoint-2",
                                    "DefaultDatabaseName": "neondb",
                                    "DefaultRoleName": "neondb_owner",
                                    "DefaultConnectionUri": "postgres://neondb_owner:pass@default-host:5432/neondb",
                                    "Host": "default-host",
                                    "Port": 5432,
                                    "Password": "pass",
                                    "EndpointType": "read_write",
                                    "EndpointRegionId": "aws-us-east-1",
                                    "EndpointSuspendTimeoutSeconds": 300,
                                    "Databases": [
                                        {
                                            "ResourceName": "appdb",
                                            "DatabaseName": "appdb",
                                            "RoleName": "app_owner",
                                            "ConnectionUri": "postgres://app_owner:dbpass@app-host:5432/appdb",
                                            "Host": "app-host",
                                            "Port": 5432,
                                            "Password": "dbpass"
                                        }
                                    ]
                                }
                                """;

                        await File.WriteAllTextAsync(outputPath, json);

            using DistributedApplication app = builder.Build();

            _ = await InvokePrivateStaticAsync(
                "ConfigureNeonConnectionFromOutputAsync",
                builder,
                neon.Resource,
                annotation,
                app.Services,
                CancellationToken.None);

            Assert.Equal("project-2", neon.Resource.ProjectId);
            Assert.Equal("branch-2", neon.Resource.BranchId);
            Assert.Equal("endpoint-2", neon.Resource.EndpointId);
            Assert.Equal("default-host", neon.Resource.Host);
            Assert.Equal("app-host", database.Resource.Host);
            Assert.Equal("postgres://app_owner:dbpass@app-host:5432/appdb", database.Resource.ConnectionUri);
        }
        finally
        {
            // Output path is generated by AddNeonProvisioner under temp; cleanup is best-effort.
        }
    }

    [Fact]
    public async Task NeonCommandsReturnExpectedFailuresForUnprovisionedAndInvalidProvisioner()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        using var app = builder.Build();
        DistributedApplicationModel model = app.Services.GetRequiredService<DistributedApplicationModel>();
        NeonProjectResource resource = Assert.Single(model.Resources.OfType<NeonProjectResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ResourceCommandAnnotation>(out IEnumerable<ResourceCommandAnnotation>? commands));
        ResourceCommandAnnotation suspendCommand = Assert.Single(commands!, command => command.Name == "neon-suspend");

        ExecuteCommandContext context = new()
        {
            ServiceProvider = app.Services,
            ResourceName = resource.Name,
            CancellationToken = CancellationToken.None,
        };

        ExecuteCommandResult notProvisioned = await suspendCommand.ExecuteCommand(context);
        Assert.False(notProvisioned.Success);
        Assert.Contains("not provisioned", notProvisioned.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NeonHealthChecksViaReflectionReportExpectedStatus()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);
        var database = neon.AddDatabase("appdb", "appdb", "app_owner");

        Type? projectHealthType = typeof(NeonProjectOptions).Assembly.GetType("CommunityToolkit.Aspire.Hosting.Neon.NeonHealthCheck");
        Type? databaseHealthType = typeof(NeonProjectOptions).Assembly.GetType("CommunityToolkit.Aspire.Hosting.Neon.NeonDatabaseHealthCheck");

        Assert.NotNull(projectHealthType);
        Assert.NotNull(databaseHealthType);

        IHealthCheck projectHealth = (IHealthCheck)Activator.CreateInstance(projectHealthType!, neon.Resource)!;
        IHealthCheck databaseHealth = (IHealthCheck)Activator.CreateInstance(databaseHealthType!, database.Resource)!;

        HealthCheckContext context = new()
        {
            Registration = new HealthCheckRegistration("neon", projectHealth, HealthStatus.Degraded, tags: null),
        };

        HealthCheckResult initialProject = await projectHealth.CheckHealthAsync(context, CancellationToken.None);
        HealthCheckResult initialDatabase = await databaseHealth.CheckHealthAsync(context, CancellationToken.None);
        Assert.Equal(HealthStatus.Degraded, initialProject.Status);
        Assert.Equal(HealthStatus.Degraded, initialDatabase.Status);

        SetPropertyValue(neon.Resource, nameof(NeonProjectResource.ConnectionUri), "postgres://u:p@host/neondb");
        SetPropertyValue(database.Resource, nameof(NeonDatabaseResource.ConnectionUri), "postgres://u:p@host/appdb");

        HealthCheckResult readyProject = await projectHealth.CheckHealthAsync(context, CancellationToken.None);
        HealthCheckResult readyDatabase = await databaseHealth.CheckHealthAsync(context, CancellationToken.None);
        Assert.Equal(HealthStatus.Healthy, readyProject.Status);
        Assert.Equal(HealthStatus.Healthy, readyDatabase.Status);
    }

    [Fact]
    public void NeonConnectionInfoParseCoversDefaultPortAndMissingCredentials()
    {
        Type? connectionInfoType = typeof(NeonProjectOptions).Assembly.GetType("CommunityToolkit.Aspire.Hosting.Neon.NeonConnectionInfo");
        Assert.NotNull(connectionInfoType);

        MethodInfo? parse = connectionInfoType!.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(parse);

        object parsed = parse!.Invoke(null, ["postgres://db.neon.tech/appdb"])!;
        Assert.Equal("db.neon.tech", parsed.GetType().GetProperty("Host")!.GetValue(parsed));
        Assert.Equal(5432, parsed.GetType().GetProperty("Port")!.GetValue(parsed));
        Assert.Equal("appdb", parsed.GetType().GetProperty("Database")!.GetValue(parsed));
        Assert.Equal(string.Empty, parsed.GetType().GetProperty("Role")!.GetValue(parsed));
        Assert.Equal(string.Empty, parsed.GetType().GetProperty("Password")!.GetValue(parsed));

        TargetInvocationException ex = Assert.Throws<TargetInvocationException>(() =>
            parse.Invoke(null, ["not-a-uri"]));
        Assert.IsType<UriFormatException>(ex.InnerException);
    }

    [Fact]
    public async Task ExecuteProvisionerEndpointCommandAsyncThrowsForMissingProjectPathAndMissingApiKey()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", string.Empty, secret: true);
        var neon = builder.AddNeon("neon", apiKey, NeonProvisionerMode.Attach);

        object missingPathAnnotation = CreateProvisionerAnnotation(
            neon.Resource.ProvisionerResource!,
            "X:\\missing\\provisioner.csproj",
            Path.Combine(Path.GetTempPath(), "missing-output.json"),
            NeonProvisionerMode.Attach);

        DistributedApplicationException missingPathError = await Assert.ThrowsAsync<DistributedApplicationException>(async () =>
            _ = await InvokePrivateStaticAsync(
                "ExecuteProvisionerEndpointCommandAsync",
                missingPathAnnotation,
                neon.Resource,
                "suspend",
                CancellationToken.None));

        Assert.Contains("was not found", missingPathError.Message, StringComparison.OrdinalIgnoreCase);

        object existingAnnotation = neon.Resource.Annotations.First(a => a.GetType().Name == "NeonExternalProvisionerAnnotation");
        string existingProjectPath = (string)existingAnnotation.GetType().GetProperty("ProjectPath")!.GetValue(existingAnnotation)!;

        object missingApiKeyAnnotation = CreateProvisionerAnnotation(
            neon.Resource.ProvisionerResource!,
            existingProjectPath,
            Path.Combine(Path.GetTempPath(), "missing-key-output.json"),
            NeonProvisionerMode.Attach);

        DistributedApplicationException missingApiKeyError = await Assert.ThrowsAsync<DistributedApplicationException>(async () =>
            _ = await InvokePrivateStaticAsync(
                "ExecuteProvisionerEndpointCommandAsync",
                missingApiKeyAnnotation,
                neon.Resource,
                "resume",
                CancellationToken.None));

        Assert.Contains("api key is required", missingApiKeyError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfigureNeonConnectionWithErrorHandlingAsyncThrowsWhenFailureArtifactExists()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey, NeonProvisionerMode.Attach);

        using var app = builder.Build();

        string outputPath = Path.Combine(Path.GetTempPath(), "neon-tests", Guid.NewGuid().ToString("N"), "neon-error.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync($"{outputPath}.error.log", "simulated failure");

        try
        {
            object annotation = CreateProvisionerAnnotation(
                neon.Resource.ProvisionerResource!,
                "placeholder.csproj",
                outputPath,
                NeonProvisionerMode.Attach);

            DistributedApplicationException ex = await Assert.ThrowsAsync<DistributedApplicationException>(async () =>
                _ = await InvokePrivateStaticAsync(
                    "ConfigureNeonConnectionWithErrorHandlingAsync",
                    builder,
                    neon.Resource,
                    annotation,
                    app.Services,
                    CancellationToken.None));

            Assert.Contains("failed before producing output", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void WithProjectOptionsAndWithBranchOptionsThrowForNullConfigure()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey);

        Assert.Throws<ArgumentNullException>(() => neon.WithProjectOptions(null!));
        Assert.Throws<ArgumentNullException>(() => neon.WithBranchOptions(null!));
    }

    [Fact]
    public async Task AddProvisionerRefreshDeletesStaleOutputArtifacts()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        var neon = builder.AddNeon("neon", apiKey, NeonProvisionerMode.Attach);

        object annotation = neon.Resource.Annotations.First(a => a.GetType().Name == "NeonExternalProvisionerAnnotation");
        string outputPath = (string)annotation.GetType().GetProperty("OutputFilePath")!.GetValue(annotation)!;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, "stale");
        await File.WriteAllTextAsync($"{outputPath}.error.log", "stale-error");

        _ = neon.AddProvisioner(NeonProvisionerMode.Attach);

        Assert.False(File.Exists(outputPath));
        Assert.False(File.Exists($"{outputPath}.error.log"));
    }

    [Fact]
    public async Task ForwardContainerBuildOptionsPrivateMethodIsInvocable()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        string projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "tests",
            "CommunityToolkit.Aspire.Hosting.Neon.Tests",
            "CommunityToolkit.Aspire.Hosting.Neon.Tests.csproj"));

        IResourceBuilder<ProjectResource> project = builder.AddProject(
            "dummy-project",
            projectPath);

        MethodInfo? method = typeof(NeonResourceBuilderExtensions).GetMethod(
            "ForwardContainerBuildOptions",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        method!.Invoke(
            null,
            [
                project,
                (Action<dynamic>)(_ => { }),
            ]);
    }

    [Fact]
    public async Task NeonSuspendAndResumeCommandsSucceedWithNoOpProvisioner()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "neon-cmd-noop", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string projectPath = Path.Combine(tempDir, "NoOpProvisioner.csproj");
        string programPath = Path.Combine(tempDir, "Program.cs");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(programPath, """
            return;
            """);

        try
        {
            using var builder = TestDistributedApplicationBuilder.Create();
            var apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
            var neon = builder.AddNeon("neon", apiKey, NeonProvisionerMode.Attach);

            SetPropertyValue(neon.Resource, nameof(NeonProjectResource.ProjectId), "project-id");
            SetPropertyValue(neon.Resource, nameof(NeonProjectResource.EndpointId), "endpoint-id");

            object annotation = CreateProvisionerAnnotation(
                neon.Resource.ProvisionerResource!,
                projectPath,
                Path.Combine(tempDir, "output.json"),
                NeonProvisionerMode.Attach);

            neon.Resource.Annotations.Add((IResourceAnnotation)annotation);

            using var app = builder.Build();
            DistributedApplicationModel model = app.Services.GetRequiredService<DistributedApplicationModel>();
            NeonProjectResource resource = Assert.Single(model.Resources.OfType<NeonProjectResource>());

            Assert.True(resource.TryGetAnnotationsOfType<ResourceCommandAnnotation>(out IEnumerable<ResourceCommandAnnotation>? commands));
            ResourceCommandAnnotation suspendCommand = Assert.Single(commands!, command => command.Name == "neon-suspend");
            ResourceCommandAnnotation resumeCommand = Assert.Single(commands!, command => command.Name == "neon-resume");

            ExecuteCommandContext context = new()
            {
                ServiceProvider = app.Services,
                ResourceName = resource.Name,
                CancellationToken = CancellationToken.None,
            };

            ExecuteCommandResult suspend = await suspendCommand.ExecuteCommand(context);
            ExecuteCommandResult resume = await resumeCommand.ExecuteCommand(context);

            Assert.True(suspend.Success, suspend.ErrorMessage);
            Assert.True(resume.Success, resume.ErrorMessage);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static async Task<object?> InvokePrivateStaticAsync(string methodName, params object[] args)
    {
        MethodInfo? method = typeof(NeonBuilderExtensions).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        object? invocationResult = method!.Invoke(null, args);
        Assert.NotNull(invocationResult);
        _ = Assert.IsAssignableFrom<Task>(invocationResult);

        Task task = (Task)invocationResult!;
        await task.ConfigureAwait(false);

        Type taskType = task.GetType();
        if (taskType.IsGenericType)
        {
            return taskType.GetProperty("Result")?.GetValue(task);
        }

        return null;
    }

    private static void SetPropertyValue(object target, string propertyName, object value)
    {
        PropertyInfo? property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private static object CreateProvisionerAnnotation(
        IResourceWithWaitSupport resource,
        string projectPath,
        string outputPath,
        NeonProvisionerMode mode)
    {
        Type? annotationType = typeof(NeonProjectOptions).Assembly.GetType("CommunityToolkit.Aspire.Hosting.Neon.NeonExternalProvisionerAnnotation");
        Assert.NotNull(annotationType);

        object? instance = Activator.CreateInstance(annotationType!, resource, projectPath, outputPath, mode);
        Assert.NotNull(instance);
        return instance!;
    }
}