using CommunityToolkit.Aspire.Hosting.Logto.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddLogtoJwtBearer("logto", "http://localhost:5072/",
        configureOptions: opt =>
        {
            opt.RequireHttpsMetadata = false;
        });

builder.Services.AddAuthorization();


var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "OK");

app.MapGet("/secure", [Authorize] (System.Security.Claims.ClaimsPrincipal user) =>
{
    return new
    {
        user.Identity?.Name,
        Claims = user.Claims.Select(c => new { c.Type, c.Value })
    };
});


app.Run();