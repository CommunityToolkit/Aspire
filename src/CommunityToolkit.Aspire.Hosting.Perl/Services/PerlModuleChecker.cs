using System.Diagnostics;

namespace CommunityToolkit.Aspire.Hosting.Perl.Services;

/// <summary>
/// Checks whether a Perl module is already installed by attempting to load it
/// with <c>perl -MModuleName -e 1</c>.
/// </summary>
internal static class PerlModuleChecker
{
    /// <summary>
    /// Checks if a Perl module is installed and loadable.
    /// </summary>
    /// <param name="perlPath">The path to the perl executable.</param>
    /// <param name="moduleName">The module name (e.g., <c>"Mojolicious"</c>, <c>"IO::Socket::SSL"</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><c>true</c> if the module loads successfully (exit code 0); otherwise <c>false</c>.</returns>
    public static async Task<bool> IsModuleInstalledAsync(
        string perlPath, string moduleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(perlPath);
        ArgumentException.ThrowIfNullOrEmpty(moduleName);

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = perlPath,
                    Arguments = $"-M{moduleName} -e 1",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // perl executable not found or other process error
            return false;
        }
    }
}
