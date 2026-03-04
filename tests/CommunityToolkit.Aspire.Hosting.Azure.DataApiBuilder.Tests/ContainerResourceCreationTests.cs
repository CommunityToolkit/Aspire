using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.Tests;
public class ContainerResourceCreationTests
{
    [Fact]
    public void AddDataAPIBuilderBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder("dab"));
    }

    [Fact]
    public void AddDataApiBuilderNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder(null!));
    }

    [Fact]
    public void AddDataAPIBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<DataApiBuilderContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("dab", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotation));
        Assert.Equal(DataApiBuilderContainerImageTags.Registry, imageAnnotation.Registry);
        Assert.Equal(DataApiBuilderContainerImageTags.Image, imageAnnotation.Image);
        Assert.Equal(DataApiBuilderContainerImageTags.Tag, imageAnnotation.Tag);

        // verify ports

        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpoints));

        var http = endpoints.Where(x => x.Name == DataApiBuilderContainerResource.HttpEndpointName).Single();
        Assert.Equal(DataApiBuilderContainerResource.HttpEndpointPort, http.TargetPort);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_NoConfigPaths_NoMounts()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // no config paths specified, no auto-default mount
        builder.AddDataAPIBuilder("dab");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.False(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out _));
    }

    [Fact]
    public void AddDataAPIBuilderContainer_PortOnly_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", httpPort: 1234);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpointAnnotations));

        var annotation = Assert.Single(endpointAnnotations);
        Assert.Equal(1234, annotation.Port);
        Assert.Equal(DataApiBuilderContainerResource.HttpEndpointPort, annotation.TargetPort);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_ValidFile_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // file exists in test project root
        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var configFileAnnotations));

        var annotation = Assert.Single(configFileAnnotations);
        Assert.EndsWith("dab-config-anonymous.json", annotation.Source);
        Assert.Equal("/App/dab-config-anonymous.json", annotation.Target);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_ValidFileWithPort_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // file exists in test project root
        builder.AddDataAPIBuilder("dab", httpPort: 1234, configFilePaths: "./dab-config-anonymous.json");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpointAnnotations));

        var annotation = Assert.Single(endpointAnnotations);
        Assert.Equal(1234, annotation.Port);
        Assert.Equal(DataApiBuilderContainerResource.HttpEndpointPort, annotation.TargetPort);

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var configFileAnnotations));

        var configAnnotation = Assert.Single(configFileAnnotations);
        Assert.EndsWith("dab-config-anonymous.json", configAnnotation.Source);
        Assert.Equal("/App/dab-config-anonymous.json", configAnnotation.Target);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_InvalidFile_ThrowsEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // file does not exist in test project root
        Assert.Throws<FileNotFoundException>(() => builder.AddDataAPIBuilder("dab", configFilePaths: Guid.NewGuid().ToString()));
    }

    [Fact]
    public void AddDataAPIBuilderContainer_ValidFiles_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // both files exist in test project root
        builder.AddDataAPIBuilder("dab", "./dab-config-anonymous.json", "./dab-config-anonymous-2.json");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var configFileAnnotations));

        Assert.Equal(2, configFileAnnotations.Count());
        Assert.Collection(
            configFileAnnotations,
            a =>
            {
                Assert.EndsWith("dab-config-anonymous.json", a.Source);
                Assert.Equal("/App/dab-config-anonymous.json", a.Target);
            },
            a =>
            {
                Assert.EndsWith("dab-config-anonymous-2.json", a.Source);
                Assert.Equal("/App/dab-config-anonymous-2.json", a.Target);
            });
    }

    [Fact]
    public void AddDataAPIBuilderContainer_InvalidFiles_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // some (not all) files exist in test project root
        Assert.Throws<FileNotFoundException>(() => builder.AddDataAPIBuilder("dab", "./dab-config-anonymous.json", "./dab-config-anonymous-2.json", Guid.NewGuid().ToString()));
    }

    [Fact]
    public void AddDataAPIBuilderContainer_HasHealthCheckAnnotation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<HealthCheckAnnotation>(out var healthCheckAnnotations));
        Assert.NotEmpty(healthCheckAnnotations);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_PrimaryEndpointIsAccessible()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var dab = builder.AddDataAPIBuilder("dab");

        Assert.NotNull(dab.Resource.PrimaryEndpoint);
        Assert.Equal(DataApiBuilderContainerResource.HttpEndpointName, dab.Resource.PrimaryEndpoint.EndpointName);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_WithImageTag_OverridesDefault()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab")
            .WithImageTag("custom-tag");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("custom-tag", imageAnnotation.Tag);
        Assert.Equal(DataApiBuilderContainerImageTags.Image, imageAnnotation.Image);
        Assert.Equal(DataApiBuilderContainerImageTags.Registry, imageAnnotation.Registry);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_WithImage_OverridesDefault()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab")
            .WithImage("custom-image");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("custom-image", imageAnnotation.Image);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_WithImageRegistry_OverridesDefault()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab")
            .WithImageRegistry("custom.registry.io");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("custom.registry.io", imageAnnotation.Registry);
        Assert.Equal(DataApiBuilderContainerImageTags.Image, imageAnnotation.Image);
        Assert.Equal(DataApiBuilderContainerImageTags.Tag, imageAnnotation.Tag);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_WithAllImageOverrides()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab")
            .WithImageRegistry("custom.registry.io")
            .WithImage("custom-image")
            .WithImageTag("custom-tag");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        Assert.Equal("custom.registry.io", imageAnnotation.Registry);
        Assert.Equal("custom-image", imageAnnotation.Image);
        Assert.Equal("custom-tag", imageAnnotation.Tag);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_BindMountsAreReadOnly()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var mountAnnotations));
        var mount = Assert.Single(mountAnnotations);
        Assert.True(mount.IsReadOnly);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_DefaultPortIsNotFixed()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpoints));
        var http = endpoints.Single(x => x.Name == DataApiBuilderContainerResource.HttpEndpointName);
        Assert.Null(http.Port);
        Assert.Equal(DataApiBuilderContainerResource.HttpEndpointPort, http.TargetPort);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_ImplementsServiceDiscovery()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var dab = builder.AddDataAPIBuilder("dab");

        Assert.IsAssignableFrom<IResourceWithServiceDiscovery>(dab.Resource);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_AuthenticatedConfig_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-authenticated.json");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var configFileAnnotations));

        var annotation = Assert.Single(configFileAnnotations);
        Assert.EndsWith("dab-config-authenticated.json", annotation.Source);
        Assert.Equal("/App/dab-config-authenticated.json", annotation.Target);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_AuthenticatedAndAnonymousConfigs_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", "./dab-config-anonymous.json", "./dab-config-authenticated.json");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var configFileAnnotations));

        Assert.Equal(2, configFileAnnotations.Count());
        Assert.Collection(
            configFileAnnotations,
            a =>
            {
                Assert.EndsWith("dab-config-anonymous.json", a.Source);
                Assert.Equal("/App/dab-config-anonymous.json", a.Target);
            },
            a =>
            {
                Assert.EndsWith("dab-config-authenticated.json", a.Source);
                Assert.Equal("/App/dab-config-authenticated.json", a.Target);
            });
    }

    [Fact]
    public void WithConfigFile_SingleFile_NoDefault_MountsCorrectly()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // Use configFilePaths to skip the default, then use WithConfigFile
        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous-2.json")
            .WithConfigFile(new FileInfo("./dab-config-authenticated.json"));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var mounts));
        Assert.Equal(2, mounts.Count());
        Assert.Contains(mounts, m => m.Target == "/App/dab-config-anonymous-2.json");
        Assert.Contains(mounts, m => m.Target == "/App/dab-config-authenticated.json");
    }

    [Fact]
    public void WithConfigFile_MultipleFiles_MountsAll()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json")
            .WithConfigFile(
                new FileInfo("./dab-config-anonymous-2.json"),
                new FileInfo("./dab-config-authenticated.json"));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var mounts));
        Assert.Equal(3, mounts.Count());
        Assert.Contains(mounts, m => m.Target == "/App/dab-config-anonymous.json");
        Assert.Contains(mounts, m => m.Target == "/App/dab-config-anonymous-2.json");
        Assert.Contains(mounts, m => m.Target == "/App/dab-config-authenticated.json");
    }

    [Fact]
    public void WithConfigFile_CalledMultipleTimes_IsAdditive()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json")
            .WithConfigFile(new FileInfo("./dab-config-anonymous-2.json"))
            .WithConfigFile(new FileInfo("./dab-config-authenticated.json"));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var mounts));
        Assert.Equal(3, mounts.Count());
    }

    [Fact]
    public void WithConfigFile_MountsAreReadOnly()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json")
            .WithConfigFile(new FileInfo("./dab-config-anonymous-2.json"));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var mounts));
        Assert.All(mounts, m => Assert.True(m.IsReadOnly));
    }

    [Fact]
    public void WithConfigFile_NonExistentFile_ThrowsFileNotFoundException()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var nonExistent = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json"));

        Assert.Throws<FileNotFoundException>(() =>
            builder.AddDataAPIBuilder("dab")
                .WithConfigFile(nonExistent));
    }

    [Fact]
    public void WithConfigFile_DuplicateFile_ThrowsInvalidOperationException()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json")
                .WithConfigFile(new FileInfo("./dab-config-anonymous.json")));

        Assert.Contains("/App/dab-config-anonymous.json", ex.Message);
        Assert.Contains("already mounted", ex.Message);
    }

    [Fact]
    public void WithConfigFile_DuplicateAcrossCalls_ThrowsInvalidOperationException()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json")
                .WithConfigFile(new FileInfo("./dab-config-anonymous-2.json"))
                .WithConfigFile(new FileInfo("./dab-config-anonymous-2.json")));

        Assert.Contains("/App/dab-config-anonymous-2.json", ex.Message);
    }

    [Fact]
    public void WithConfigFile_NullBuilder_ThrowsArgumentNullException()
    {
        IResourceBuilder<DataApiBuilderContainerResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() =>
            builder.WithConfigFile(new FileInfo("./dab-config-anonymous.json")));
    }


    [Fact]
    public void WithConfigFolder_MountsAllFilesInDirectory()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json")
            .WithConfigFolder(new DirectoryInfo("./config-folder"));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var mounts));

        // 1 from AddDataAPIBuilder + 2 from config-folder
        Assert.Equal(3, mounts.Count());
        Assert.Contains(mounts, m => m.Target == "/App/dab-config-anonymous.json");
        Assert.Contains(mounts, m => m.Target == "/App/dab-folder-config-anonymous.json");
        Assert.Contains(mounts, m => m.Target == "/App/dab-folder-config-anonymous-2.json");
    }

    [Fact]
    public void WithConfigFolder_MountsAreReadOnly()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json")
            .WithConfigFolder(new DirectoryInfo("./config-folder"));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var mounts));
        Assert.All(mounts, m => Assert.True(m.IsReadOnly));
    }

    [Fact]
    public void WithConfigFolder_MountsIndividualFilesNotFolders()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json")
            .WithConfigFolder(new DirectoryInfo("./config-folder"));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var mounts));

        // Every mount target should be a file path (not a directory)
        Assert.All(mounts, m =>
        {
            Assert.StartsWith("/App/", m.Target);
            Assert.Contains(".", m.Target); // has a file extension
            Assert.Equal(ContainerMountType.BindMount, m.Type);
        });
    }

    [Fact]
    public void WithConfigFolder_CalledMultipleTimes_IsAdditive()
    {
        // Create a temp directory with a unique file to avoid collisions
        string tempDir = Path.Combine(Path.GetTempPath(), "dab-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string tempFile = Path.Combine(tempDir, "dab-temp-config.json");
        File.WriteAllText(tempFile, "{}");

        try
        {
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

            builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json")
                .WithConfigFolder(new DirectoryInfo("./config-folder"))
                .WithConfigFolder(new DirectoryInfo(tempDir));

            using var app = builder.Build();

            var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
            var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

            Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var mounts));

            // 1 default + 2 from config-folder + 1 from tempDir
            Assert.Equal(4, mounts.Count());
            Assert.Contains(mounts, m => m.Target == "/App/dab-temp-config.json");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void WithConfigFolder_NonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var nonExistent = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        Assert.Throws<DirectoryNotFoundException>(() =>
            builder.AddDataAPIBuilder("dab")
                .WithConfigFolder(nonExistent));
    }

    [Fact]
    public void WithConfigFolder_EmptyDirectory_IsNoOp()
    {
        string emptyDir = Path.Combine(Path.GetTempPath(), "dab-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDir);

        try
        {
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

            builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json")
                .WithConfigFolder(new DirectoryInfo(emptyDir));

            using var app = builder.Build();

            var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
            var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

            Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var mounts));

            // Only the default config file mount
            Assert.Single(mounts);
        }
        finally
        {
            Directory.Delete(emptyDir, true);
        }
    }

    [Fact]
    public void WithConfigFolder_DuplicateWithExistingMount_ThrowsInvalidOperationException()
    {
        // config-folder contains dab-folder-config-anonymous.json - use WithConfigFile to mount it first, then folder
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var folderDir = new DirectoryInfo("./config-folder");
        var fileInFolder = folderDir.GetFiles().First();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json")
                .WithConfigFile(new FileInfo(fileInFolder.FullName))
                .WithConfigFolder(folderDir));

        Assert.Contains("already mounted", ex.Message);
    }

    [Fact]
    public void WithConfigFolder_NullBuilder_ThrowsArgumentNullException()
    {
        IResourceBuilder<DataApiBuilderContainerResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() =>
            builder.WithConfigFolder(new DirectoryInfo("./config-folder")));
    }

    [Fact]
    public void WithConfigFile_And_WithConfigFolder_Combined()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config-anonymous.json")
            .WithConfigFile(new FileInfo("./dab-config-authenticated.json"))
            .WithConfigFolder(new DirectoryInfo("./config-folder"));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var mounts));

        // 1 from AddDataAPIBuilder + 1 from WithConfigFile + 2 from WithConfigFolder
        Assert.Equal(4, mounts.Count());
        Assert.Contains(mounts, m => m.Target == "/App/dab-config-anonymous.json");
        Assert.Contains(mounts, m => m.Target == "/App/dab-config-authenticated.json");
        Assert.Contains(mounts, m => m.Target == "/App/dab-folder-config-anonymous.json");
        Assert.Contains(mounts, m => m.Target == "/App/dab-folder-config-anonymous-2.json");
    }
}
