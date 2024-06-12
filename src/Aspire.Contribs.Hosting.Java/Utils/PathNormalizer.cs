namespace Aspire.Contribs.Hosting.Java.Utils;

/// <summary>
/// This represents the utility entity for normalizing paths.
/// </summary>
internal static class PathNormalizer
{
    /// <summary>
    /// Normalizes the path for the current platform.
    /// </summary>
    /// <param name="path">The path value.</param>
    /// <returns>Returns the normalized path value for the current platform.</returns>
    public static string NormalizePathForCurrentPlatform(this string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        // Fix slashes
        path = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        return Path.GetFullPath(path);
    }
}
