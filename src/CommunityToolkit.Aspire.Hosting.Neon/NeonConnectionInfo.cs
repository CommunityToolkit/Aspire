namespace CommunityToolkit.Aspire.Hosting.Neon;

internal readonly record struct NeonConnectionInfo(
    string Host,
    int Port,
    string Database,
    string Role,
    string Password)
{
    public static NeonConnectionInfo Parse(string connectionUri)
    {
        Uri uri = new(connectionUri);
        var database = uri.AbsolutePath.TrimStart('/');
        var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        var role = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var host = uri.Host;
        var port = uri.IsDefaultPort ? 5432 : uri.Port;

        return new NeonConnectionInfo(host, port, database, role, password);
    }
}
