using CommunityToolkit.Aspire.Hosting.Dbx;
using System.Text.Json.Serialization;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents the type of database for a dbx connection.
/// </summary>
[JsonConverter(typeof(DbxDatabaseTypeJsonConverter))]
public enum DbxDatabaseType
{
    /// <summary>MySQL database.</summary>
    [JsonPropertyName("mysql")]
    Mysql,

    /// <summary>PostgreSQL database.</summary>
    [JsonPropertyName("postgres")]
    Postgres,

    /// <summary>SQLite database.</summary>
    [JsonPropertyName("sqlite")]
    Sqlite,

    /// <summary>Redis key-value store.</summary>
    [JsonPropertyName("redis")]
    Redis,

    /// <summary>DuckDB analytical database.</summary>
    [JsonPropertyName("duckdb")]
    DuckDb,

    /// <summary>ClickHouse columnar database.</summary>
    [JsonPropertyName("clickhouse")]
    ClickHouse,

    /// <summary>Microsoft SQL Server database.</summary>
    [JsonPropertyName("sqlserver")]
    SqlServer,

    /// <summary>MongoDB document database.</summary>
    [JsonPropertyName("mongodb")]
    MongoDb,

    /// <summary>Oracle database.</summary>
    [JsonPropertyName("oracle")]
    Oracle,

    /// <summary>Elasticsearch search engine.</summary>
    [JsonPropertyName("elasticsearch")]
    Elasticsearch,

    /// <summary>Apache Doris analytical database.</summary>
    [JsonPropertyName("doris")]
    Doris,

    /// <summary>StarRocks analytical database.</summary>
    [JsonPropertyName("starrocks")]
    StarRocks,

    /// <summary>Amazon Redshift data warehouse.</summary>
    [JsonPropertyName("redshift")]
    Redshift,

    /// <summary>Dameng database.</summary>
    [JsonPropertyName("dameng")]
    Dameng,

    /// <summary>Kingbase database.</summary>
    [JsonPropertyName("kingbase")]
    Kingbase,

    /// <summary>HighGo database.</summary>
    [JsonPropertyName("highgo")]
    Highgo,

    /// <summary>VastBase database.</summary>
    [JsonPropertyName("vastbase")]
    Vastbase,

    /// <summary>GoldenDB database.</summary>
    [JsonPropertyName("goldendb")]
    Goldendb,

    /// <summary>GaussDB database.</summary>
    [JsonPropertyName("gaussdb")]
    Gaussdb,

    /// <summary>YashanDB database.</summary>
    [JsonPropertyName("yashandb")]
    Yashandb,

    /// <summary>Databricks lakehouse platform.</summary>
    [JsonPropertyName("databricks")]
    Databricks,

    /// <summary>SAP HANA in-memory database.</summary>
    [JsonPropertyName("saphana")]
    SapHana,

    /// <summary>Teradata data warehouse.</summary>
    [JsonPropertyName("teradata")]
    Teradata,

    /// <summary>Vertica analytical database.</summary>
    [JsonPropertyName("vertica")]
    Vertica,

    /// <summary>Firebird relational database.</summary>
    [JsonPropertyName("firebird")]
    Firebird,

    /// <summary>Exasol analytical database.</summary>
    [JsonPropertyName("exasol")]
    Exasol,

    /// <summary>openGauss database.</summary>
    [JsonPropertyName("opengauss")]
    OpenGauss,

    /// <summary>OceanBase Oracle-compatible database.</summary>
    [JsonPropertyName("oceanbase-oracle")]
    OceanbaseOracle,

    /// <summary>GBase database.</summary>
    [JsonPropertyName("gbase")]
    Gbase,

    /// <summary>Microsoft Access database.</summary>
    [JsonPropertyName("access")]
    Access,

    /// <summary>H2 embedded database.</summary>
    [JsonPropertyName("h2")]
    H2,

    /// <summary>Snowflake cloud data warehouse.</summary>
    [JsonPropertyName("snowflake")]
    Snowflake,

    /// <summary>Trino distributed query engine.</summary>
    [JsonPropertyName("trino")]
    Trino,

    /// <summary>Apache Hive data warehouse.</summary>
    [JsonPropertyName("hive")]
    Hive,

    /// <summary>IBM Db2 database.</summary>
    [JsonPropertyName("db2")]
    Db2,

    /// <summary>IBM Informix database.</summary>
    [JsonPropertyName("informix")]
    Informix,

    /// <summary>Neo4j graph database.</summary>
    [JsonPropertyName("neo4j")]
    Neo4j,

    /// <summary>Apache Cassandra NoSQL database.</summary>
    [JsonPropertyName("cassandra")]
    Cassandra,

    /// <summary>Google BigQuery data warehouse.</summary>
    [JsonPropertyName("bigquery")]
    Bigquery,

    /// <summary>Apache Kylin OLAP engine.</summary>
    [JsonPropertyName("kylin")]
    Kylin,

    /// <summary>SunDB database.</summary>
    [JsonPropertyName("sundb")]
    Sundb,

    /// <summary>TDengine time-series database.</summary>
    [JsonPropertyName("tdengine")]
    Tdengine,

    /// <summary>Xugu database.</summary>
    [JsonPropertyName("xugu")]
    Xugu,

    /// <summary>InterSystems IRIS data platform.</summary>
    [JsonPropertyName("iris")]
    Iris,

    /// <summary>Generic JDBC connection.</summary>
    [JsonPropertyName("jdbc")]
    Jdbc,
}
