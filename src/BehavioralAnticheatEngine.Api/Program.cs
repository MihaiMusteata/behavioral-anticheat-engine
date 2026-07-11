var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapHealthChecks("/health");

app.MapGet("/api/status", () =>
{
    return Results.Ok(new
    {
        service = "Behavioral Anticheat Engine API",
        status = "running",
        timestamp = DateTimeOffset.UtcNow
    });
})
.WithName("GetApiStatus");

app.Run();
