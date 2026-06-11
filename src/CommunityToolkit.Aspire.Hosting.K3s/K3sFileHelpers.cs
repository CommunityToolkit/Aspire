namespace CommunityToolkit.Aspire.Hosting;

/// <summary>
/// Shared constants for Unix file modes and well-known in-container paths used by
/// k3s-related containers.
/// </summary>
internal static class K3sFileHelpers
{
    /// <summary>
    /// The path inside every container that receives a k3s kubeconfig via a file-level
    /// bind-mount from the host's <c>AppHostDirectory/.k3s/{name}/container/kubeconfig.yaml</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Placing the file under <c>/tmp/</c> (which is guaranteed to exist in all POSIX
    /// containers) rather than inside <c>~/.kube/</c> ensures that <c>kubectl</c>'s
    /// cache directories (<c>~/.kube/cache/</c>, <c>~/.kube/http-cache/</c>) are created
    /// <em>inside</em> the container's ephemeral filesystem rather than in the host-side
    /// <c>container/</c> directory. This prevents:
    /// <list type="bullet">
    ///   <item>Host filesystem pollution with kubectl cache directories.</item>
    ///   <item>Cache corruption when multiple helm or kubectl containers run concurrently
    ///         and share the same host-side mount directory.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Using a <em>file-level</em> bind-mount (source file → target file, not directory →
    /// directory) means only the kubeconfig YAML is visible inside the container at this
    /// path; any other files the host may later add to <c>container/</c> are not exposed.
    /// </para>
    /// </remarks>
    internal const string ContainerKubeconfigPath = "/tmp/k3s-kubeconfig.yaml";

    /// <summary>
    /// Unix file mode <c>0755</c> (<c>rwxr-xr-x</c>) for shell scripts injected into
    /// k3s, <c>alpine/helm</c>, and <c>alpine/kubectl</c> containers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why <c>0755</c> and not a narrower mode like <c>0700</c>?</b>
    /// </para>
    /// <para>
    /// The scripts are invoked as <c>/bin/sh /script.sh</c> — the shell reads the file,
    /// so the execute bit on the script itself is technically not required for the current
    /// invocation pattern. We set it anyway for two reasons:
    /// <list type="bullet">
    ///   <item>
    ///     <b>Convention:</b> scripts intended to be executed are conventionally marked
    ///     executable. If the invocation is later changed to direct execution
    ///     (<c>./script.sh</c>), the permission is already correct without a
    ///     <c>chmod</c> inside the container.
    ///   </item>
    ///   <item>
    ///     <b>Custom image overrides:</b> <c>K3sClusterOptions</c> lets callers replace
    ///     the helm/kubectl images. Non-root container users in those custom images need
    ///     <c>OtherRead | OtherExecute</c> to read and execute the injected scripts
    ///     when the file is owned by a different UID/GID. The default images
    ///     (<c>alpine/helm</c>, <c>alpine/kubectl</c>, <c>rancher/k3s</c>) all run as
    ///     <c>root</c> (UID 0), so <c>UserRead | UserExecute</c> alone would suffice for
    ///     the defaults — but <c>0755</c> is the safe choice for the general case.
    ///   </item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Why <c>UserWrite</c>?</b>
    /// <c>WithContainerFiles</c> calls <c>docker cp</c> which sets the destination owner
    /// to root. Without <c>UserWrite</c>, root cannot overwrite the file on a subsequent
    /// AppHost restart (when the same container image layer is re-used), leading to a
    /// silent stale-script failure. This is the same reason <c>chmod 644</c> rather than
    /// <c>444</c> is conventional for data files on Linux.
    /// </para>
    /// </remarks>
    internal const UnixFileMode ExecutableScriptMode =
        UnixFileMode.UserRead  | UnixFileMode.UserWrite  | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
}
