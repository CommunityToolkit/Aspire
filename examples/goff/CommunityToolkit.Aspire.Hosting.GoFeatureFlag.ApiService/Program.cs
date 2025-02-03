using OpenFeature.Contrib.Providers.GOFeatureFlag;
using OpenFeature.Model;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddGoFeatureFlagClient("goff");

var app = builder.Build();

app.MapDefaultEndpoints();

// Currently supported flags are:
// - `display-banner`
app.MapGet(
    "/features/{featureName}", 
    async (string featureName, GoFeatureFlagProvider provider, CancellationToken cancellationToken) =>
    {
        var userContext = EvaluationContext.Builder()
            .Set("targetingKey", Guid.NewGuid().ToString())
            .Set("anonymous", true)
            .Build();
        var flag = await provider.ResolveBooleanValueAsync(featureName, false, userContext, cancellationToken);
        
        return Results.Ok(flag);
    })
    .WithName("GetFeature");

app.Run();