using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
const string apiAudience = "http://127.0.0.1:9234/";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddLogtoJwtBearer("logto", appIdentification: apiAudience,
        configureOptions: opt =>
        {
            opt.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
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
