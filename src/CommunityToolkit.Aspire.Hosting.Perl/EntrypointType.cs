namespace CommunityToolkit.Aspire.Hosting.Perl;

/// <summary>
/// Defines the type of entrypoint for a Perl application.
/// 
/// Supported entrypoint types currently include Script, API, Module, and Executable.
/// </summary> 
public enum EntrypointType
{
    /// <summary>
    /// A direct executable file to run (e.g., a PAR-packed binary or compiled Perl application).
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
    /// A Perl API script to listen for HTTP requests.
    /// </summary>
    API
}