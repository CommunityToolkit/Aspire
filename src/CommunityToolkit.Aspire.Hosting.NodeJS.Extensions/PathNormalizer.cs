namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions;

// Copied from https://github.com/dotnet/aspire/blob/50ca9fa670af5c70782dc75d2961956b06f1a403/src/Shared/PathNormalizer.cs
internal static class PathNormalizer
{
    public static string NormalizePathForCurrentPlatform(string path)
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
