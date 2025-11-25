# Docker Registry Integration for Aspire

## Overview

This document describes the Docker Registry integration for the Aspire application host. The registry provides a local container registry for storing and serving Docker images, supporting both building images from Dockerfiles and importing existing images.

Why would you need this? 

If you are building a service that serves docker images, or for some reason you need to run k8s in Aspire you can use this registry as the registry for your local cluster.

## Usage

### Basic Setup

```csharp
// In AppHost.cs
var registry = builder.AddDockerRegistry("local-registry");
```

### Adding Images from Dockerfiles

```csharp
registry.WithRegistryDockerfile(
    imageName: "your-image-name",
    imageNamePrefix: "your/custom/image/prefix"
    dockerfileContext: "../../mcp/mcp_terminal",
    tag: "latest"
);
```

### Adding Existing Images

```csharp
registry.WithRegistryImage(
    imageName: "your-image-name",
    imageNamePrefix: "your/custom/image/prefix",
    sourceImage: "redis:alpine",
    tag: "custom"
);
```

### Integration with Docker API Service

```csharp
var dockerApi = builder.AddDockerApiResource(registry);
```

## Implementation Details

### Annotations

The `DockerRegistryImageAnnotation` class tracks image metadata:

- `ImageName`: Name of the image in the local registry
- `Tag`: Image tag (default: "latest")
- `DockerfileContext`: Path to Dockerfile context (for builds)
- `SourceImage`: Source image to pull (for existing images)
- `IsPushed`: Track if image has been pushed to registry

### Lifecycle Hooks (Aspire 9.4)

The new Aspire 9.4 lifecycle hooks provide better control:

1. **OnInitializeResource**: Set up initial configuration
2. **OnBeforeResourceStarted**: Prepare registry settings
3. **OnResourceEndpointsAllocated**: Capture allocated ports
4. **OnResourceReady**: Execute image builds and pushes

### Registry Configuration

- **Port**: Dynamic allocation using Aspire's endpoint system
- **Storage**: Scoped persistent volume at `/var/lib/registry`
- **Volume Name**: to support layer caching
- **Authentication**: None (local development only)
- **Health Check**: HTTP endpoint at `/v2/`


### Custom Build Strategies

Implement custom build strategies by extending the image processing:

```csharp
public interface IImageBuildStrategy
{
    Task BuildAsync(DockerRegistryImageAnnotation annotation, string registryUrl);
}
```

## Error Handling

The registry integration handles several error scenarios:

1. **Registry Startup Failure**: Retry with exponential backoff
2. **Build Failures**: Log error and mark image as failed
3. **Push Failures**: Retry with configurable attempts
4. **Network Issues**: Configurable timeout and retry policies

## Security Considerations

For production deployments:

1. Enable TLS for registry communication
2. Implement authentication (basic auth or token-based)
3. Use image signing and verification
4. Implement access control policies
5. Regular security scanning of stored images

## Performance Optimization

1. **Parallel Builds**: Build multiple images concurrently (limited to 3)
2. **Layer Caching**: Leverage Docker's build cache
3. **Compression**: Enable registry compression
4. **Garbage Collection**: Configure periodic cleanup

## Troubleshooting

### Common Issues

1. **Registry not accessible**: Check port binding and firewall rules
2. **Build failures**: Verify Dockerfile paths and build context
3. **Push failures**: Check registry health and disk space
4. **Image not found**: Verify image name and tag format

### Debug Commands

```bash
# Check registry catalog
curl http://localhost:<port>/v2/_catalog

# Check image tags
curl http://localhost:<port>/v2/<image>/tags/list

# Test registry health
curl http://localhost:<port>/v2/
```

## Future Enhancements

1. **Multi-architecture builds**: Support for ARM and other platforms
2. **Registry mirroring**: Cache upstream images locally
3. **Web UI**: Add registry browser interface
4. **Metrics**: Prometheus metrics for monitoring
5. **Replication**: Support for registry clustering
