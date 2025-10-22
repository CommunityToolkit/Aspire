# InfluxDB Integration Example

This example demonstrates how to use the .NET Aspire InfluxDB hosting and client integrations.

## Overview

The solution consists of:

-   **InfluxDB.AppHost**: The Aspire AppHost that orchestrates the application and InfluxDB container
-   **InfluxDB.ServiceDefaults**: Common service defaults for the application
-   **InfluxDB.ApiService**: A minimal API service that demonstrates reading and writing data to InfluxDB

## Running the Example

1. Ensure Docker is running on your machine
2. Navigate to the `InfluxDB.AppHost` directory
3. Run the application:
    ```bash
    dotnet run
    ```
4. The Aspire Dashboard will open, showing the running services

## API Endpoints

The ApiService exposes two endpoints:

### Write Data

`GET /write`

Writes sample temperature and humidity data points to InfluxDB. Returns a confirmation with the number of points written.

Example response:

```json
{
    "message": "Data written successfully",
    "points": 3
}
```

### Read Data

`GET /read`

Queries and returns all temperature and humidity measurements from the last hour.

Example response:

```json
{
  "count": 3,
  "data": [
    {
      "time": "2025-10-16T12:34:56.789Z",
      "measurement": "temperature",
      "location": "server-room",
      "field": "value",
      "value": 22.5
    },
    ...
  ]
}
```

## How It Works

### AppHost Configuration

The AppHost sets up an InfluxDB container and configures the API service to reference it:

```csharp
var influxdb = builder.AddInfluxDB("influxdb");

builder.AddProject<InfluxDB_ApiService>("apiservice")
    .WithReference(influxdb)
    .WaitFor(influxdb);
```

### Client Integration

The API service uses the InfluxDB client integration to connect to the database:

```csharp
builder.AddInfluxDBClient(connectionName: "influxdb");
```

The client is then injected into endpoints and used to write and query data using the InfluxDB Client API.

## Learn More

-   [InfluxDB Client Documentation](https://github.com/influxdata/influxdb-client-csharp)
-   [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
-   [Community Toolkit Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/community-toolkit/overview)

