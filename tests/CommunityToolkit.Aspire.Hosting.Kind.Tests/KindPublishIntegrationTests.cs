// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001 // Pipeline APIs are experimental

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindPublishIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task PublishContainerResourceProducesHelmChart()
    {
        var tempDir = Directory.CreateTempSubdirectory(".kind-publish-test");
        output.WriteLine($"Temp directory: {tempDir.FullName}");

        try
        {
            using var builder = PublishTestBuilder.CreateForPublish(tempDir.FullName);

            builder.AddKubernetesEnvironment("k8s").WithKind();
            builder.AddContainer("redis", "redis", "7");

            using var app = builder.Build();
            await app.RunAsync();

            var chartYaml = File.ReadAllText(Path.Combine(tempDir.FullName, "Chart.yaml"));
            var valuesYaml = File.ReadAllText(Path.Combine(tempDir.FullName, "values.yaml"));
            var deploymentYaml = File.ReadAllText(Path.Combine(tempDir.FullName, "templates", "redis", "deployment.yaml"));

            await Verify(chartYaml, "yaml")
                .AppendContentAsFile(valuesYaml, "yaml")
                .AppendContentAsFile(deploymentYaml, "yaml");
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PublishWithServiceCustomizationAppliesType()
    {
        var tempDir = Directory.CreateTempSubdirectory(".kind-publish-customize-test");
        output.WriteLine($"Temp directory: {tempDir.FullName}");

        try
        {
            using var builder = PublishTestBuilder.CreateForPublish(tempDir.FullName);

            builder.AddKubernetesEnvironment("k8s").WithKind();
            builder.AddContainer("redis", "redis", "7")
                .WithHttpEndpoint(targetPort: 6379)
                .PublishAsKubernetesService(k8s =>
                {
                    k8s.Service!.Spec.Type = "NodePort";
                });

            using var app = builder.Build();
            await app.RunAsync();

            var serviceYaml = File.ReadAllText(Path.Combine(tempDir.FullName, "templates", "redis", "service.yaml"));
            var deploymentYaml = File.ReadAllText(Path.Combine(tempDir.FullName, "templates", "redis", "deployment.yaml"));

            await Verify(serviceYaml, "yaml")
                .AppendContentAsFile(deploymentYaml, "yaml");
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PublishMultipleContainersProducesAllDeployments()
    {
        var tempDir = Directory.CreateTempSubdirectory(".kind-publish-multi-test");
        output.WriteLine($"Temp directory: {tempDir.FullName}");

        try
        {
            using var builder = PublishTestBuilder.CreateForPublish(tempDir.FullName);

            builder.AddKubernetesEnvironment("k8s").WithKind();
            builder.AddContainer("cache", "redis", "7");
            builder.AddContainer("web", "nginx", "latest")
                .WithHttpEndpoint(targetPort: 80);

            using var app = builder.Build();
            await app.RunAsync();

            Assert.True(Directory.Exists(Path.Combine(tempDir.FullName, "templates", "cache")));
            Assert.True(Directory.Exists(Path.Combine(tempDir.FullName, "templates", "web")));

            var valuesYaml = File.ReadAllText(Path.Combine(tempDir.FullName, "values.yaml"));
            var webDeployment = File.ReadAllText(Path.Combine(tempDir.FullName, "templates", "web", "deployment.yaml"));
            var webService = File.ReadAllText(Path.Combine(tempDir.FullName, "templates", "web", "service.yaml"));

            await Verify(valuesYaml, "yaml")
                .AppendContentAsFile(webDeployment, "yaml")
                .AppendContentAsFile(webService, "yaml");
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }
}
