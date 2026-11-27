using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

/// <summary>
/// Resolves and copies DACPAC artifacts used by pipeline steps.
/// </summary>
internal static class DacpacPipelineArtifacts
{
    public static async Task<string> GetConsumerPathAsync(
        IResourceWithDacpac resource,
        DacpacPipelineEffectiveOptions options,
        PipelineStepContext context,
        string buildOutputPath)
    {
        if (options.Source == DacpacSource.Build)
        {
            return ValidateFile(resource, buildOutputPath);
        }

        if (options.Source == DacpacSource.File)
        {
            if (!resource.TryGetLastAnnotation<DacpacPipelinePathParameterAnnotation>(out var parameter))
            {
                throw new DistributedApplicationException(
                    $"DACPAC path parameter for resource '{resource.Name}' is unavailable.");
            }

            var value = await parameter.Parameter.GetValueAsync(context.CancellationToken);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new DistributedApplicationException(
                    $"Parameter '{parameter.Parameter.Name}' does not specify a DACPAC path for resource '{resource.Name}'.");
            }

            var environment = context.Services.GetRequiredService<IHostEnvironment>();
            var path = Path.IsPathRooted(value)
                ? value
                : Path.GetFullPath(value, environment.ContentRootPath);
            return ValidateFile(resource, path);
        }

        var resolver = context.Services.GetRequiredService<SqlProjectDacpacResolver>();
        using var dacpac = options.Source == DacpacSource.Existing
            ? await resolver.ResolveExistingAsync(resource, context.Logger, context.CancellationToken)
            : await resolver.ResolveAsync(resource, context.Logger, context.CancellationToken);
        return ValidateFile(resource, dacpac.Path);
    }

    /// <summary>
    /// Copies a DACPAC so readers of <paramref name="destinationPath"/> never observe a partial file.
    /// </summary>
    public static void Copy(string sourcePath, string destinationPath)
    {
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);
        if (!File.Exists(source))
        {
            throw new DistributedApplicationException($"DACPAC '{source}' was not found.");
        }

        if (string.Equals(source, destination, GetPathComparison()))
        {
            return;
        }

        var destinationDirectory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = Path.Combine(destinationDirectory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(source, temporaryPath);
            File.Move(temporaryPath, destination, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
    private static string ValidateFile(IResourceWithDacpac resource, string path)
    {
        if (!File.Exists(path))
        {
            throw new DistributedApplicationException(
                $"DACPAC for resource '{resource.Name}' was not found at '{path}'.");
        }

        return path;
    }

    private static StringComparison GetPathComparison()
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
