using Renci.SshNet;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSftpClient("sftp", cfg =>
{
    cfg.Username = "foo";
    cfg.Password = "pass";
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

const string fileName = "uploads/hello.txt";

app.MapPost("/upload", async (SftpClient client, CancellationToken cancellationToken) =>
{
    try
    {
        var tokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        await client.ConnectAsync(tokenSource.Token);

        var fileContent = Encoding.UTF8.GetBytes("Hello world!");

        using var inputStream = new MemoryStream(fileContent);

        await client.UploadFileAsync(inputStream, fileName);

        return Results.File(inputStream.ToArray());
    }
    catch (Exception ex)
    {
        return Results.InternalServerError(ex.ToString());
    }
    finally
    {
        client.Disconnect();
    }
});

app.MapGet("/download", async (SftpClient client, CancellationToken cancellationToken) =>
{
    try
    {
        var tokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        await client.ConnectAsync(tokenSource.Token);

        using var outputStream = new MemoryStream();

        await client.DownloadFileAsync(fileName, outputStream);

        return Results.File(outputStream.ToArray());
    }
    catch (Exception ex)
    {
        return Results.InternalServerError(ex.ToString());
    }
    finally
    {
        client.Disconnect();
    }
});

app.Run();
