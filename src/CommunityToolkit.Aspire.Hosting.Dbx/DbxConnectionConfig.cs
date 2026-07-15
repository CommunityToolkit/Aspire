using System.Text.Json.Serialization;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a database connection configuration for dbx.
/// </summary>
public sealed class DbxConnectionConfig
{
    /// <summary>Gets or sets the unique identifier for the connection.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>Gets or sets the display name of the connection.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>Gets or sets the database type.</summary>
    [JsonPropertyName("db_type")]
    public required DbxDatabaseType DbType { get; set; }

    /// <summary>Gets or sets optional URL parameters.</summary>
    [JsonPropertyName("url_params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UrlParams { get; set; }

    /// <summary>Gets or sets the host address.</summary>
    [JsonPropertyName("host")]
    public required string Host { get; set; }

    /// <summary>Gets or sets the port number.</summary>
    [JsonPropertyName("port")]
    public required ushort Port { get; set; }

    /// <summary>Gets or sets the username for authentication.</summary>
    [JsonPropertyName("username")]
    public required string Username { get; set; }

    /// <summary>Gets or sets the password for authentication.</summary>
    [JsonPropertyName("password")]
    public required string Password { get; set; }

    /// <summary>Gets or sets the database name.</summary>
    [JsonPropertyName("database")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Database { get; set; }

    /// <summary>Gets or sets the connection timeout in seconds.</summary>
    [JsonPropertyName("connect_timeout_secs")]
    public ulong ConnectTimeoutSecs { get; set; } = 30;

    /// <summary>Gets or sets the query timeout in seconds.</summary>
    [JsonPropertyName("query_timeout_secs")]
    public ulong QueryTimeoutSecs { get; set; } = 30;

    /// <summary>Gets or sets whether SSL is enabled.</summary>
    [JsonPropertyName("ssl")]
    public bool Ssl { get; set; }

    /// <summary>Gets or sets an explicit connection string, overriding individual fields.</summary>
    [JsonPropertyName("connection_string")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConnectionString { get; set; }

    /// <summary>Gets or sets the Redis connection mode (e.g. "standalone", "sentinel", "cluster").</summary>
    [JsonPropertyName("redis_connection_mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RedisConnectionMode { get; set; }

    /// <summary>Gets or sets the Redis Sentinel master name.</summary>
    [JsonPropertyName("redis_sentinel_master")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RedisSentinelMaster { get; set; }

    /// <summary>Gets or sets the Redis Sentinel nodes (comma-separated).</summary>
    [JsonPropertyName("redis_sentinel_nodes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RedisSentinelNodes { get; set; }

    /// <summary>Gets or sets the Redis Sentinel username.</summary>
    [JsonPropertyName("redis_sentinel_username")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RedisSentinelUsername { get; set; }

    /// <summary>Gets or sets the Redis Sentinel password.</summary>
    [JsonPropertyName("redis_sentinel_password")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RedisSentinelPassword { get; set; }

    /// <summary>Gets or sets whether TLS is enabled for Redis Sentinel.</summary>
    [JsonPropertyName("redis_sentinel_tls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RedisSentinelTls { get; set; }

    /// <summary>Gets or sets the Redis cluster nodes (comma-separated).</summary>
    [JsonPropertyName("redis_cluster_nodes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RedisClusterNodes { get; set; }

    /// <summary>Gets or sets the JDBC driver class.</summary>
    [JsonPropertyName("jdbc_driver_class")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? JdbcDriverClass { get; set; }

    /// <summary>Gets or sets the JDBC driver paths.</summary>
    [JsonPropertyName("jdbc_driver_paths")]
    public List<string> JdbcDriverPaths { get; set; } = [];
}
