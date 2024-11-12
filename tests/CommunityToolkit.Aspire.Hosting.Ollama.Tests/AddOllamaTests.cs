using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Ollama.Tests;

public class AddOllamaTests
{
    [Fact]
    public void VerifyCustomModel()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ollama = builder.AddOllama("ollama", port: null);
        var model = ollama.AddModel("custom:tag");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var ollamaResource = Assert.Single(appModel.Resources.OfType<OllamaResource>());
        var modelResource = Assert.Single(appModel.Resources.OfType<OllamaModelResource>());

        Assert.Equal("ollama", ollamaResource.Name);
        Assert.Contains("custom:tag", ollamaResource.Models);

        Assert.Equal("ollama-custom", modelResource.Name);
        Assert.Equal("custom:tag", modelResource.ModelName);
        Assert.Equal(ollamaResource, modelResource.Parent);
    }

    [Fact]
    public void VerifyDefaultPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama", port: null);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>());

        Assert.Equal(11434, endpoint.TargetPort);
    }

    [Fact]
    public void VerifyCustomPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddOllama("ollama", port: 12345);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>());

        Assert.Equal(12345, endpoint.Port);
    }

    [Fact]
    public void CanSetMultpleModels()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ollama = builder.AddOllama("ollama", port: null);

        var llama3 = ollama.AddModel("llama3");
        var phi3 = ollama.AddModel("phi3");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var ollamaResource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        var modelResources = appModel.Resources.OfType<OllamaModelResource>();

        Assert.Equal("ollama", ollamaResource.Name);

        Assert.Contains("llama3", ollamaResource.Models);
        Assert.Contains("phi3", ollamaResource.Models);

        Assert.Collection(modelResources,
        model =>
        {
            Assert.Equal("ollama-llama3", model.Name);
            Assert.Equal("llama3", model.ModelName);
            Assert.Equal(ollamaResource, model.Parent);
        },
        model =>
        {
            Assert.Equal("ollama-phi3", model.Name);
            Assert.Equal("phi3", model.ModelName);
            Assert.Equal(ollamaResource, model.Parent);
        });
    }

    [Fact]
    public void DistributedApplicationBuilderCannotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() => DistributedApplication.CreateBuilder().AddOllama(null!, port: null));
    }

    [Fact]
    public void ResourceNameCannotBeOmitted()
    {
        string name = "";
        Assert.Throws<ArgumentException>(() => DistributedApplication.CreateBuilder().AddOllama(name, port: null));

        name = " ";
        Assert.Throws<ArgumentException>(() => DistributedApplication.CreateBuilder().AddOllama(name, port: null));

        name = null!;
        Assert.Throws<ArgumentNullException>(() => DistributedApplication.CreateBuilder().AddOllama(name, port: null));
    }

    [Fact]
    public void ModelNameCannotBeOmmitted()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ollama = builder.AddOllama("ollama", port: null);

        string name = "";
        Assert.Throws<ArgumentException>(() => ollama.AddModel(name));

        name = " ";
        Assert.Throws<ArgumentException>(() => ollama.AddModel(name));

        name = null!;
        Assert.Throws<ArgumentNullException>(() => ollama.AddModel(name));
    }

    [Fact]
    public void OpenWebUIConfigured()
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddOllama("ollama", port: null).WithOpenWebUI();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OpenWebUIResource>());

        Assert.Equal("ollama-openwebui", resource.Name);
        Assert.Equal("http", resource.PrimaryEndpoint.EndpointName);
        Assert.Equal(8080, resource.PrimaryEndpoint.TargetPort);

        Assert.True(resource.TryGetAnnotationsOfType<ContainerImageAnnotation>(out var imageAnnotations));
        var imageAnnotation = Assert.Single(imageAnnotations);
        Assert.Equal(OllamaContainerImageTags.OpenWebUIImage, imageAnnotation.Image);
        Assert.Equal(OllamaContainerImageTags.OpenWebUITag, imageAnnotation.Tag);
        Assert.Equal(OllamaContainerImageTags.OpenWebUIRegistry, imageAnnotation.Registry);

        Assert.False(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out _));
    }

    [Theory]
    [InlineData("volumeName")]
    [InlineData(null)]
    public void CanPersistVolumeOfOpenWebUI(string? volumeName)
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddOllama("ollama", port: null).WithOpenWebUI(configureContainer: container =>
        {
            container.WithDataVolume(volumeName);
        });

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OpenWebUIResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.Equal("/app/backend/data", annotation.Target);

        if (volumeName is null)
        {
            Assert.NotNull(annotation.Source);
        }
        else
        {
            Assert.Equal(volumeName, annotation.Source);
        }
    }

    [Fact]
    public void NoDataVolumeNameGeneratesOne()
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddOllama("ollama").WithDataVolume();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);

        Assert.NotNull(annotation.Source);
    }

    [Fact]
    public void SpecifiedDataVolumeNameIsUsed()
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddOllama("ollama").WithDataVolume("data");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);

        Assert.Equal("data", annotation.Source);
    }

    [Theory]
    [InlineData("data")]
    [InlineData(null)]
    public void CorrectTargetPathOnVolumeMount(string? volumeName)
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddOllama("ollama").WithDataVolume(volumeName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);

        Assert.Equal("/root/.ollama", annotation.Target);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadOnlyVolumeMount(bool isReadOnly)
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddOllama("ollama").WithDataVolume(isReadOnly: isReadOnly);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);

        Assert.Equal(isReadOnly, annotation.IsReadOnly);
    }

    [Theory]
    [InlineData("hf.co/bartowski/Llama-3.2-1B-Instruct-GGUF:IQ4_XS")]
    [InlineData("hf.co/bartowski/Llama-3.2-1B-Instruct-GGUF:IQ4_XS@sha256:1234567890abcdef")]
    [InlineData("huggingface.co/bartowski/Llama-3.2-1B-Instruct-GGUF:IQ4_XS")]
    [InlineData("huggingface.co/bartowski/Llama-3.2-1B-Instruct-GGUF:IQ4_XS@sha256:1234567890abcdef")]
    public void HuggingFaceModel(string modelName)
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddOllama("ollama").AddHuggingFaceModel("llama", modelName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        var modelResource = Assert.Single(appModel.Resources.OfType<OllamaModelResource>());

        Assert.Equal("ollama", resource.Name);
        Assert.Contains(modelName, resource.Models);

        Assert.Equal("llama", modelResource.Name);
        Assert.Equal(modelName, modelResource.ModelName);
        Assert.Equal(resource, modelResource.Parent);
    }

    [Fact]
    public void HuggingFaceModelWithoutDomainPrefixHasItAdded()
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddOllama("ollama").AddHuggingFaceModel("llama", "bartowski/Llama-3.2-1B-Instruct-GGUF:IQ4_XS");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        var modelResource = Assert.Single(appModel.Resources.OfType<OllamaModelResource>());

        Assert.Equal("ollama", resource.Name);
        Assert.Contains("hf.co/bartowski/Llama-3.2-1B-Instruct-GGUF:IQ4_XS", resource.Models);

        Assert.Equal("llama", modelResource.Name);
        Assert.Equal("hf.co/bartowski/Llama-3.2-1B-Instruct-GGUF:IQ4_XS", modelResource.ModelName);
        Assert.Equal(resource, modelResource.Parent);
    }

    [Fact]
    public void OllamaRegistersHttpHealthCheck()
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddOllama("ollama");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        Assert.True(resource.TryGetAnnotationsOfType<HealthCheckAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);
        Assert.Contains("http", annotation.Key);
        Assert.Contains("/", annotation.Key);
        Assert.Contains(resource.Name, annotation.Key);
    }

    [Fact]
    public void OllamaRegistrationContainsResourceCommandAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();
        _ = builder.AddOllama("ollama");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<OllamaResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ResourceCommandAnnotation>(out var annotations));

        Assert.Equal(2, annotations.Count());

        Assert.Collection(annotations,
            annotation =>
            {
                Assert.Equal("ListAllModels", annotation.Type);
                Assert.Equal("List All Models", annotation.DisplayName);
                Assert.Equal("List all models in the Ollama container.", annotation.DisplayDescription);
                Assert.Equal("AppsList", annotation.IconName);
            },
            annotation =>
            {
                Assert.Equal("ListRunningModels", annotation.Type);
                Assert.Equal("List Running Models", annotation.DisplayName);
                Assert.Equal("List all running models in the Ollama container.", annotation.DisplayDescription);
                Assert.Equal("AppsList", annotation.IconName);
            });
    }

    [Fact]
    public void OllamaModelResourceRegistersCustomHealthCheck()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ollama = builder.AddOllama("ollama");
        _ = ollama.AddModel("custom:tag");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var modelResource = Assert.Single(appModel.Resources.OfType<OllamaModelResource>());

        Assert.True(modelResource.TryGetAnnotationsOfType<HealthCheckAnnotation>(out var annotations));

        var annotation = Assert.Single(annotations);
        Assert.Contains(modelResource.Name, annotation.Key);
        Assert.Contains(modelResource.ModelName, annotation.Key);
    }

    [Fact]
    public void OllamaModelResourceRegistersResourceCommandAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();
        var ollama = builder.AddOllama("ollama");
        _ = ollama.AddModel("custom:tag");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var modelResource = Assert.Single(appModel.Resources.OfType<OllamaModelResource>());

        Assert.True(modelResource.TryGetAnnotationsOfType<ResourceCommandAnnotation>(out var annotations));

        Assert.Equal(4, annotations.Count());

        Assert.Collection(annotations,
            annotation =>
            {
                Assert.Equal("Redownload", annotation.Type);
                Assert.Equal("Redownload Model", annotation.DisplayName);
                Assert.Equal("Redownload the model custom:tag.", annotation.DisplayDescription);
                Assert.Equal("ArrowDownload", annotation.IconName);
                Assert.False(annotation.IsHighlighted);
            },
            annotation =>
            {
                Assert.Equal("Delete", annotation.Type);
                Assert.Equal("Delete Model", annotation.DisplayName);
                Assert.Equal("Delete the model custom:tag.", annotation.DisplayDescription);
                Assert.Equal("Delete", annotation.IconName);
                Assert.False(annotation.IsHighlighted);
            },
            annotation =>
            {
                Assert.Equal("ModelInfo", annotation.Type);
                Assert.Equal("Print Model Info", annotation.DisplayName);
                Assert.Equal("Print the info for the model custom:tag.", annotation.DisplayDescription);
                Assert.Equal("Info", annotation.IconName);
                Assert.False(annotation.IsHighlighted);
            },
            annotation =>
            {
                Assert.Equal("Stop", annotation.Type);
                Assert.Equal("Stop Model", annotation.DisplayName);
                Assert.Equal("Stop the model custom:tag.", annotation.DisplayDescription);
                Assert.Equal("Stop", annotation.IconName);
                Assert.True(annotation.IsHighlighted);
            });
    }
}
