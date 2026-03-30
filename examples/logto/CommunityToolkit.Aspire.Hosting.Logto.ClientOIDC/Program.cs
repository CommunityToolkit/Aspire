using CommunityToolkit.Aspire.Hosting.Logto.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.AddLogtoOIDC("logto", logtoOptions: config =>
{
    config.AppId = "s6zda5bqn1qlsjzaiklqn";
    config.AppSecret = "Df77aDt13MG3nSTgo8eKZP2HdeSfbed0";
    
},oidcOptions: opt =>
{
    opt.RequireHttpsMetadata = false;
});
builder.Services.AddAuthorization();

var app = builder.Build();


app.UseAuthentication();
app.UseAuthorization();




app.MapGet("/", () => "Hello World!");

app.MapGet("/me",
        [Authorize](ClaimsPrincipal user) => new
        {
            Name = user.Identity?.Name,
            IsAuthenticated = user.Identity?.IsAuthenticated ?? false,
            Claims = user.Claims.Select(c => new { c.Type, c.Value })
        })
    .WithName("Me");

app.MapGet("/signin", async context =>
{
    if (!(context.User?.Identity?.IsAuthenticated ?? false))
    {
        await context.ChallengeAsync(new AuthenticationProperties { RedirectUri = "/me" });
    }
    else
    {
        context.Response.Redirect("/me");
    }
});
app.MapGet("/tokens", [Authorize] async (HttpContext ctx) =>
{
    var accessToken = await ctx.GetTokenAsync("access_token");
    var idToken = await ctx.GetTokenAsync("id_token");
    var refreshToken = await ctx.GetTokenAsync("refresh_token");

    return Results.Ok(new
    {
        access_token = accessToken,
        id_token = idToken,
        refresh_token = refreshToken
    });
});

app.MapGet("/signout", async context =>
{
    if (context.User?.Identity?.IsAuthenticated ?? false)
    {
        await context.SignOutAsync(new AuthenticationProperties { RedirectUri = "/" });
    }
    else
    {
        context.Response.Redirect("/");
    }
});
app.Run();