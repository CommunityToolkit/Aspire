using System.Text.Json.Serialization;

namespace Aspire.Hosting.ApplicationModel;

// Blocked: ROAD-1 — Full debugging support requires ExecutableLaunchConfiguration
// from Aspire.Hosting.Dcp.Model, which is currently internal.
// See plans/perl-feature-gap.md §ROAD-1 for details.
internal sealed class PerlLaunchConfiguration()
{
    [JsonPropertyName("program_path")]
    public string ProgramPath { get; set; } = string.Empty;

    [JsonPropertyName("module")]
    public string Module { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public string Mode {get;set; } = "development";

    [JsonPropertyName("interpreter_path")]
    public string InterpreterPath { get; set; } = string.Empty;
}