#pragma warning disable ASPIRECOMPUTE003
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using Aspire.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Resolves the Vercel project identity for a workload, preferring existing linked project
/// metadata and otherwise creating stable Aspire-managed names for repeatable deploy/destroy.
/// </summary>
internal static class VercelProjectNameResolver
{
    public static string GetProjectName(VercelDeploymentEntry entry)
        => GetProjectLink(entry).ProjectName;

    public static string GetProjectName(IResource resource)
    {
        if (resource.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var dockerfile))
        {
            return GetProjectName(new VercelDeploymentEntry(resource, dockerfile.ContextPath, dockerfile.DockerfilePath, dockerfile));
        }

        if (resource is ProjectResource project)
        {
            string projectPath = project.GetProjectMetadata().ProjectPath;
            string sourceRoot = Path.GetDirectoryName(projectPath)
                ?? throw new DistributedApplicationException($"Project resource '{resource.Name}' has project path '{projectPath}' without a containing directory.");
            return GetProjectName(new VercelDeploymentEntry(resource, sourceRoot));
        }

        throw new DistributedApplicationException($"Resource '{resource.Name}' targets Vercel but is not an Aspire image build resource. Use a .NET project, a workload integration that publishes Dockerfile metadata, call PublishAsDockerFile, or configure the resource with WithDockerfile, WithDockerfileFactory, or WithDockerfileBuilder.");
    }

    public static VercelProjectLink GetProjectLink(VercelDeploymentEntry entry)
    {
        if (TryReadProjectLink(entry.SourceRoot, out var projectLink))
        {
            return projectLink;
        }

        return new(GetManagedProjectName(entry), ProjectId: null);
    }

    public static string GetProjectOption(VercelDeploymentEntry entry)
    {
        var projectLink = GetProjectLink(entry);
        return string.IsNullOrWhiteSpace(projectLink.ProjectId)
            ? projectLink.ProjectName
            : projectLink.ProjectId;
    }

    public static bool HasProjectLinkFile(string sourceRoot)
        => File.Exists(GetProjectJsonPath(sourceRoot));

    public static bool IsValidProjectName(string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName)
            || projectName.Length > VercelConstants.ProjectNameMaxLength
            || !IsLowercaseAsciiLetterOrDigit(projectName[0])
            || !IsLowercaseAsciiLetterOrDigit(projectName[^1]))
        {
            return false;
        }

        return projectName.All(static character =>
            IsLowercaseAsciiLetterOrDigit(character)
            || character == '-');
    }

    private static string GetManagedProjectName(VercelDeploymentEntry entry)
    {
        if (entry.Resource.TryGetLastAnnotation<VercelProjectOptionsAnnotation>(out var options))
        {
            return options.ProjectName;
        }

        // The production endpoint contract is project-name based, so managed names must
        // be stable and Vercel-valid before deploy starts.
        // See https://vercel.com/docs/projects/overview.
        string sourceRoot = Path.TrimEndingDirectorySeparator(entry.SourceRoot);
        string sourceRootName = Path.GetFileName(sourceRoot);

        if (TryCreateProjectName(sourceRootName, out string? projectName)
            || TryCreateProjectName(entry.Resource.Name, out projectName))
        {
            return projectName;
        }

        throw new DistributedApplicationException($"Could not infer a valid Vercel project name for resource '{entry.Resource.Name}' from source root '{entry.SourceRoot}'. Rename the source directory or link the source root to an existing Vercel project.");
    }

    private static bool TryCreateProjectName(string? value, [NotNullWhen(true)] out string? projectName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            projectName = null;
            return false;
        }

        var builder = new StringBuilder(value.Length);
        bool previousWasSeparator = false;

        foreach (char character in value)
        {
            if (IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        projectName = builder
            .ToString()
            .Trim('-');

        if (projectName.Length > VercelConstants.ProjectNameMaxLength)
        {
            projectName = projectName[..VercelConstants.ProjectNameMaxLength].Trim('-');
        }

        if (projectName.Length == 0)
        {
            projectName = null;
            return false;
        }

        return true;
    }

    private static bool TryReadProjectLink(string sourceRoot, [NotNullWhen(true)] out VercelProjectLink? projectLink)
    {
        string projectJsonPath = GetProjectJsonPath(sourceRoot);

        if (File.Exists(projectJsonPath))
        {
            // Vercel CLI writes linked project identity as:
            //   .vercel/project.json: { "projectId": "...", "orgId": "...", "projectName": "..." }
            // Treat it as user/provider ownership metadata rather than regenerating a managed
            // name. Destroy preserves these linked projects and only removes tracked env vars.
            // See https://vercel.com/docs/cli/link.
            var project = JsonSerializer.Deserialize<VercelLinkedProjectJson>(File.ReadAllText(projectJsonPath));
            if (project is { ProjectName: { } projectName } && !string.IsNullOrWhiteSpace(projectName))
            {
                projectLink = new(projectName, project.ProjectId);
                return true;
            }
        }

        projectLink = null;
        return false;
    }

    private static string GetProjectJsonPath(string sourceRoot)
        => Path.Combine(sourceRoot, VercelConstants.DirectoryName, VercelConstants.ProjectFileName);

    private static bool IsAsciiLetterOrDigit(char character)
        => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static bool IsLowercaseAsciiLetterOrDigit(char character)
        => character is >= 'a' and <= 'z' or >= '0' and <= '9';

    private sealed class VercelLinkedProjectJson
    {
        [JsonPropertyName("projectName")]
        public string? ProjectName { get; init; }

        [JsonPropertyName("projectId")]
        public string? ProjectId { get; init; }
    }
}
