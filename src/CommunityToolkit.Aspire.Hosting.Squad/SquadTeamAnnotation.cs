namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Carries Squad team metadata on a <see cref="SquadResource"/> so that lifecycle hooks,
/// dashboard annotations, and downstream tooling know where team files live.
/// </summary>
public sealed class SquadTeamAnnotation : IResourceAnnotation
{
    /// <summary>Gets the absolute path to the directory that contains the <c>.squad/</c> folder.</summary>
    public string TeamRoot { get; }

    /// <summary>Gets the absolute path to <c>.squad/decisions.md</c>.</summary>
    public string DecisionsMdPath { get; }

    /// <summary>Gets the absolute path to <c>.squad/decisions/inbox/</c>.</summary>
    public string InboxDir { get; }

    /// <summary>Gets the absolute path to <c>.squad/team.md</c> (the agent roster).</summary>
    public string AgentRosterFile { get; }

    /// <summary>
    /// Initialises a new <see cref="SquadTeamAnnotation"/>.
    /// </summary>
    public SquadTeamAnnotation(string teamRoot, string decisionsMdPath, string inboxDir, string agentRosterFile)
    {
        ArgumentException.ThrowIfNullOrEmpty(teamRoot);

        TeamRoot = teamRoot;
        DecisionsMdPath = decisionsMdPath;
        InboxDir = inboxDir;
        AgentRosterFile = agentRosterFile;
    }
}
