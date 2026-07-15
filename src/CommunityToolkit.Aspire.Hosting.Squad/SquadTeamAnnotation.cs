namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Carries Squad team metadata on a <see cref="SquadResource"/> so that lifecycle hooks,
/// dashboard annotations, and downstream tooling know where team files live.
/// </summary>
/// <param name="teamRoot">The absolute path to the directory that contains the <c>.squad/</c> folder.</param>
/// <param name="decisionsMdPath">The absolute path to <c>.squad/decisions.md</c>.</param>
/// <param name="inboxDir">The absolute path to <c>.squad/decisions/inbox/</c>.</param>
/// <param name="agentRosterFile">The absolute path to <c>.squad/team.md</c> (the agent roster).</param>
public sealed class SquadTeamAnnotation(string teamRoot, string decisionsMdPath, string inboxDir, string agentRosterFile) : IResourceAnnotation
{
    /// <summary>Gets the absolute path to the directory that contains the <c>.squad/</c> folder.</summary>
    public string TeamRoot { get; } = teamRoot ?? throw new ArgumentNullException(nameof(teamRoot));

    /// <summary>Gets the absolute path to <c>.squad/decisions.md</c>.</summary>
    public string DecisionsMdPath { get; } = decisionsMdPath;

    /// <summary>Gets the absolute path to <c>.squad/decisions/inbox/</c>.</summary>
    public string InboxDir { get; } = inboxDir;

    /// <summary>Gets the absolute path to <c>.squad/team.md</c> (the agent roster).</summary>
    public string AgentRosterFile { get; } = agentRosterFile;
}
