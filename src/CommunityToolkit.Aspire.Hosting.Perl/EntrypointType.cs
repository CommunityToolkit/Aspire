namespace CommunityToolkit.Aspire.Hosting.Perl;

/// <summary>
/// Defines the type of entrypoint for a Perl application.
/// 
/// Currently only Script is supported, but this enum is designed to be extensible for future entrypoint types.
/// </summary> 
public enum EntrypointType
{
    /// <summary>
    /// A direct executable file to run (e.g., a compiled binary).  I believe
    /// it is rare, but perl can compile to a binary executable...
    /// </summary>
    Executable,

    /// <summary>
    /// A Perl script file to execute directly (e.g., "main.pl", "app.pl").
    /// </summary>
    Script,

    /// <summary>
    /// A Perl module to run as the main application (e.g., "MyApp::Main").
    /// </summary>
    Module,

    /// <summary>
    /// A Perl one-liner command to execute.
    /// </summary>
    OneLiner,

    /// <summary>
    /// A Perl API script to listen for HTTP requests.
    /// </summary>
    API
}