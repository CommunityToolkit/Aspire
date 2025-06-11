using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var supabase = builder.AddSupabase("supabase");

var api = builder.AddProject<Projects.CommunityToolkit_Aspire_Supabase_Api>("api")
    .WithReference(supabase)
    .WaitFor(supabase);

builder.Build().Run();