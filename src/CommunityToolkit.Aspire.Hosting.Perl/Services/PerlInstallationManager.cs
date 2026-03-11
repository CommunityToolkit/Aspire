using System.Diagnostics;

namespace CommunityToolkit.Aspire.Hosting.Perl.Services;

/// <summary>
/// Validates that a Perl installation is available and functional by running <c>perl -v</c>
/// and checking that the output starts with "This is perl".
/// </summary>
internal class PerlInstallationManager
{
    /// <summary>
    /// Validates that the Perl executable at the given path produces expected output.
    /// </summary>
    /// <param name="perlPath">The resolved path to the perl executable.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><c>true</c> if the output of <c>perl -v</c> starts with "This is perl"; otherwise <c>false</c>.</returns>
    public async Task<bool> IsPerlInstalledAsync(string perlPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(perlPath);

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = perlPath,
                    Arguments = "-v",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return output.TrimStart().StartsWith("This is perl", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }
}