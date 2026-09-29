using System.Text;

namespace CommunityToolkit.Aspire.Hosting;

/// <summary>
/// Helpers for composing the POSIX shell scripts that are generated on the host and
/// injected into the Linux helper containers (<c>alpine/helm</c>, <c>alpine/kubectl</c>).
/// </summary>
internal static class K3sShellScript
{
    /// <summary>
    /// Appends <paramref name="line"/> followed by a single LF (<c>\n</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Never use <see cref="StringBuilder.AppendLine(string)"/> for generated shell
    /// scripts.</b> It appends <see cref="Environment.NewLine"/>, which is CRLF on Windows.
    /// The scripts are executed by busybox <c>ash</c> inside a Linux container, and busybox
    /// does not strip the trailing CR: a line such as <c>if [ ... ]; then</c> is tokenized
    /// as <c>then\r</c>, which is not the <c>then</c> keyword. The <c>if</c> is therefore
    /// never closed and the script aborts at EOF with
    /// <c>syntax error: unexpected end of file (expecting "then")</c>.
    /// </para>
    /// <para>
    /// The bug is invisible on Linux and macOS build agents (where
    /// <see cref="Environment.NewLine"/> is already LF), so it only ever surfaces for
    /// developers running the AppHost on Windows.
    /// </para>
    /// </remarks>
    internal static StringBuilder AppendShellLine(this StringBuilder builder, string line) =>
        builder.Append(line).Append('\n');
}
