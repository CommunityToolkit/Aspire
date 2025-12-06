using CommunityToolkit.Aspire.Hosting.Logto.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.AddLogtoOIDC("logto", logtoOptions: config =>
{
    config.AppId = "1oy1oel4jjk0vo1yzton0";
    config.AppSecret = "1vYKGbSQ2QXvyf24lJy1cUDFKjrDdNxQ";
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

// Маршрут логаута
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