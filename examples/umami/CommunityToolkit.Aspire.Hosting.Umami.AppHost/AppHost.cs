using System.Net.Http.Headers;
using System.Net.Http.Json;

var builder = DistributedApplication.CreateBuilder(args);

const string websiteId = "204ea2a3-d77d-400e-9e6d-0db81de293f3";

var password = builder.AddParameter("db-password", "12345678");

var postgres = builder
    .AddPostgres("postgres", password: password, port: 61118)
    .WithLifetime(ContainerLifetime.Persistent);
var postgresdb = postgres.AddDatabase("postgresdb");

var umami = builder
    .AddUmami("umami", port: 55932)
    .WithPostgreSQL(postgresdb)
    .OnResourceReady(async (resource, _, ct) =>
    {
        var umamiEndpoint = await resource.GetEndpoint("http").GetValueAsync(ct).ConfigureAwait(false);

        using var umamiApiHttpClient = new HttpClient();
        umamiApiHttpClient.BaseAddress = new Uri(umamiEndpoint!);

        const string defaultUmamiUser = "admin";
        const string defaultUmamiPassword = "umami";

        var loginResponseMessage = await umamiApiHttpClient.PostAsJsonAsync(
            "/api/auth/login",
            new UmamiAuthLoginPayload(defaultUmamiUser, defaultUmamiPassword),
            ct
        );

        loginResponseMessage.EnsureSuccessStatusCode();
        var loginResponse = await loginResponseMessage.Content.ReadFromJsonAsync<UmamiAuthLoginResponse>(ct);
        var token = loginResponse?.Token;
        if (token is null)
        {
            throw new Exception("Token not found");
        }
        
        umamiApiHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var existingWebsite = await umamiApiHttpClient.GetFromJsonAsync<UmamiGetWebsiteResponse>(
            $"/api/websites/{websiteId}",
            ct
        );
        if (existingWebsite is not null)
        {
            return;
        }
        
        var createWebsiteResponseMessage = await umamiApiHttpClient.PostAsJsonAsync(
            "/api/websites",
            new UmamiCreateWebsitePayload(websiteId, "localhost", "localhost"),
            ct
        );
        createWebsiteResponseMessage.EnsureSuccessStatusCode();
    });

var front = builder
    .AddJavaScriptApp("front", "../umami-vite-app")
    .WithEndpoint(name: "http", scheme: "http", env: "PORT")
    .WaitFor(umami)
    .WithEnvironment("VITE_UMAMI_ENDPOINT", umami.GetEndpoint("http"))
    .WithEnvironment("VITE_UMAMI_WEBSITE_ID", websiteId);

builder.Build().Run();

public record UmamiAuthLoginPayload(string Username, string Password);
public record UmamiAuthLoginResponse(string Token);
public record UmamiGetWebsiteResponse(string Id, string Name, string Domain);
public record UmamiCreateWebsitePayload(string Id, string Name, string Domain);