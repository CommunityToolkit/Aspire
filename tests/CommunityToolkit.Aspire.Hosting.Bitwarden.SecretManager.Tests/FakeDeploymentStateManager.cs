#pragma warning disable ASPIREPIPELINES002

using Aspire.Hosting.Pipelines;
using System.Text.Json.Nodes;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager.Tests;

internal sealed class FakeDeploymentStateManager : IDeploymentStateManager
{
    private readonly Dictionary<string, DeploymentStateSection> _acquired =
        new(StringComparer.OrdinalIgnoreCase);

    public List<string> SavedSectionNames { get; } = [];

    public string StateFilePath => Path.GetTempPath();

    public Task<DeploymentStateSection> AcquireSectionAsync(
        string sectionName, CancellationToken cancellationToken)
    {
        var section = new DeploymentStateSection(sectionName, new JsonObject(), 0);
        _acquired[sectionName] = section;
        return Task.FromResult(section);
    }

    public Task SaveSectionAsync(DeploymentStateSection section, CancellationToken cancellationToken)
    {
        SavedSectionNames.Add(section.SectionName);
        return Task.CompletedTask;
    }

    public Task DeleteSectionAsync(DeploymentStateSection _, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task ClearAllStateAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    // Returns the value stored by SetValue("…") — DeploymentStateSection stores it under the empty-string key.
    public string? GetSavedValue(string sectionName)
        => _acquired.TryGetValue(sectionName, out var section)
            ? section.Data[""]?.GetValue<string>()
            : null;
}

#pragma warning restore ASPIREPIPELINES002
