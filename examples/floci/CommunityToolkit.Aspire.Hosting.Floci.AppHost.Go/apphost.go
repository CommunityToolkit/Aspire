package main

import (
	"apphost/modules/aspire"
	"crypto/rand"
	"fmt"
	"log"
	"os"
	"path/filepath"
)

func main() {
	builder, err := aspire.CreateBuilder()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	aws := builder.AddFlociAws("floci-aws")
	azure := builder.AddFlociAzure("floci-az")
	gcp := builder.AddFlociGcp("floci-gcp")
	namespace := make([]byte, 16)
	if _, err := rand.Read(namespace); err != nil {
		log.Fatal(err)
	}
	azure.WithDockerSocket().WithEnvironment("FLOCI_AZ_DOCKER_RESOURCE_NAMESPACE", fmt.Sprintf("sample-%x", namespace))
	serviceBus := azure.WithServiceBus(&aspire.WithServiceBusOptions{Name: aspire.StringPtr("servicebus")})
	_ = serviceBus.AmqpEndpoint()
	_ = serviceBus.AmqpTlsEndpoint()
	_ = serviceBus.ConnectionStringExpression()
	storage := azure.WithStorage(&aspire.WithStorageOptions{Name: aspire.StringPtr("storage")})
	if _, err := storage.AccountName(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	_ = storage.ServiceEndpoint()
	_ = storage.ConnectionStringExpression()
	aws.WithFlociUI(&aspire.WithFlociUIOptions{
		ConfigureContainer: func(ui aspire.FlociUIContainerResource) {
			ui.WithAzureReference(azure).WithGcpReference(gcp)
			if err := ui.Err(); err != nil {
				log.Fatal(aspire.FormatError(err))
			}
		},
	})
	api := builder.AddDotnetProject("floci-api", "../CommunityToolkit.Aspire.Hosting.Floci.ApiService/CommunityToolkit.Aspire.Hosting.Floci.ApiService.csproj").
		WithExternalHttpEndpoints().
		WithHttpHealthCheck(&aspire.WithHttpHealthCheckOptions{Path: aspire.StringPtr("/health")}).
		WithFlociAwsReference(aws).WithFlociAzureReference(azure).WithFlociGcpReference(gcp).
		WithFlociAzureServiceBusReference(serviceBus).WithReference(storage).
		WaitFor(aws).WaitFor(azure).WaitFor(gcp).WaitFor(storage)
	probe := builder.AddContainer("servicebus-probe", "node:22-alpine").
		WithFlociAzureServiceBusReference(serviceBus).
		WithHttpEndpoint(&aspire.WithHttpEndpointOptions{TargetPort: aspire.Float64Ptr(8080)}).
		WithHttpHealthCheck(&aspire.WithHttpHealthCheckOptions{Path: aspire.StringPtr("/")}).
		WaitFor(azure).WithArgs([]string{"-e", `
        const net = require('node:net');
        const http = require('node:http');
        const assert = require('node:assert/strict');
        const connectionString = process.env.ConnectionStrings__servicebus;
        const endpoint = new URL(connectionString.split(';')[0].slice('Endpoint='.length));
        const header = Buffer.from([65, 77, 81, 80, 0, 1, 0, 0]);
        let ready = false;
        const socket = net.connect({ host: endpoint.hostname, port: Number(endpoint.port) }, () => socket.write(header));
        socket.setTimeout(20000, () => { throw new Error('AMQP handshake timed out'); });
        let response = Buffer.alloc(0);
        socket.on('data', data => {
            response = Buffer.concat([response, data]);
            if (response.length >= header.length) {
                assert.deepEqual(response.subarray(0, header.length), header);
                ready = true;
                socket.destroy();
            }
        });
        http.createServer((request, response) => {
            response.writeHead(ready ? 200 : 503);
            response.end();
        }).listen(8080, '0.0.0.0');
    `})
	custom := builder.AddFlociAws("floci-custom", &aspire.AddFlociAwsOptions{
		Port: aspire.Float64Ptr(14566), DefaultRegion: aspire.StringPtr("eu-west-1"), DefaultAccountId: aspire.StringPtr("123456789012"),
	})
	gcpCustom := builder.AddFlociGcp("floci-gcp-custom", &aspire.AddFlociGcpOptions{DefaultProjectId: aspire.StringPtr("my-project")})
	persistent := builder.AddFlociAws("floci-persistent").WithDataVolume("floci-data")
	dataPath, err := filepath.Abs(filepath.Join(".aspire", "floci-data"))
	if err != nil {
		log.Fatal(err)
	}
	if err := os.MkdirAll(dataPath, 0755); err != nil {
		log.Fatal(err)
	}
	mount := builder.AddFlociAws("floci-mount").WithDataBindMount(dataPath)

	const includeCompileOnlyScenarios = false
	if includeCompileOnlyScenarios {
		builder.AddFlociAws("floci-with-ui").WithFlociUI(&aspire.WithFlociUIOptions{
			ContainerName: aspire.StringPtr("my-floci-ui"),
			ConfigureContainer: func(ui aspire.FlociUIContainerResource) {
				ui.WithHostPort(aspire.Float64Ptr(14500))
				if err := ui.Err(); err != nil {
					log.Fatal(aspire.FormatError(err))
				}
			},
		})
		socketOptions := &aspire.WithDockerSocketOptions{SocketPath: aspire.StringPtr("/run/user/1000/podman/podman.sock")}
		builder.AddFlociAws("floci-podman").WithDockerSocket(socketOptions)
		builder.AddFlociAzure("floci-az-podman").WithDockerSocket(socketOptions).
			WithServiceBus(&aspire.WithServiceBusOptions{Name: aspire.StringPtr("podman-servicebus"), AmqpPort: aspire.Float64Ptr(15673), AmqpTlsPort: aspire.Float64Ptr(15674)})
		builder.AddFlociGcp("floci-gcp-podman").WithDockerSocket(socketOptions)
		builder.AddFlociAzure("floci-az-persistent").WithDataVolume("floci-az-data")
		builder.AddFlociAzure("floci-az-mount").WithDataBindMount("/tmp/floci-az-data")
		builder.AddFlociGcp("floci-gcp-persistent").WithDataVolume("floci-gcp-data")
		builder.AddFlociGcp("floci-gcp-mount").WithDataBindMount("/tmp/floci-gcp-data")
		builder.AddFlociAws("floci-tls").WithHttpsDeveloperCertificate()
		builder.AddFlociAzure("floci-az-tls").WithHttpsDeveloperCertificate()
		builder.AddFlociAws("floci-configured").WithConfigFile("./floci.yml")
		_ = aws.PrimaryEndpoint()
		_ = aws.Host()
		_ = aws.Port()
		_ = aws.ConnectionStringExpression()
		_ = azure.PrimaryEndpoint()
		_ = azure.Host()
		_ = azure.Port()
		_ = azure.ConnectionStringExpression()
		_ = gcp.PrimaryEndpoint()
		_ = gcp.Host()
		_ = gcp.Port()
		_ = gcp.ConnectionStringExpression()
		builder.AddDotnetProject("api", "../FlociApi/FlociApi.csproj").
			WithFlociAwsReference(aws).WithFlociAzureReference(azure).WithFlociGcpReference(gcp)
		builder.AddContainer("worker", "myorg/worker").
			WithFlociAwsReference(aws).WithFlociAzureReference(azure).WithFlociGcpReference(gcp)
	}
	for _, resource := range []interface{ Err() error }{aws, azure, gcp, serviceBus, storage, api, probe, custom, gcpCustom, persistent, mount} {
		if err := resource.Err(); err != nil {
			log.Fatal(aspire.FormatError(err))
		}
	}
	app, err := builder.Build()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := app.Run(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
