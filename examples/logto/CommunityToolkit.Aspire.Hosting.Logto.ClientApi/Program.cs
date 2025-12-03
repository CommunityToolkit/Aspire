using CommunityToolkit.Aspire.Hosting.Logto.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.AddLogtoOIDC("logto", logtoOptions: config =>
{
    config.AppId = "8wnwpd8smxq51ebgd5lv1";
    config.AppSecret = "SkOrdgpWNftsQlxAX7JD5gT5oospwOZ9";
});

var app = builder.Build();


app.UseHttpsRedirection();


app.Run();


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