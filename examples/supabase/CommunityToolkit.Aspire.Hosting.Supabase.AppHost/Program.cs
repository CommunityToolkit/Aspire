using CommunityToolkit.Aspire.Hosting.Supabase.Builders;

var builder = DistributedApplication.CreateBuilder(args);

// Add a complete Supabase stack
// This includes: PostgreSQL, Auth (GoTrue), REST API (PostgREST),
// Storage, Kong API Gateway, Postgres-Meta, and Studio Dashboard
var supabase = builder.AddSupabase("supabase");

// Optional: Configure individual components
supabase
    .ConfigureAuth(auth => auth
        .WithSiteUrl("http://localhost:3000")
        .WithAutoConfirm(true)
        .WithAnonymousUsers(true))
    .ConfigureStorage(storage => storage
        .WithFileSizeLimit(100_000_000)  // 100MB
        .WithImageTransformation(true))
    .ConfigureDatabase(db => db
        .WithPassword("your-secure-password")
        .WithPort(54322))
    .WithMigrations("<path_to_migrations_folder>")
    .WithEdgeFunctions("<path_to_edge_functions_folder>");

builder.Build().Run();
