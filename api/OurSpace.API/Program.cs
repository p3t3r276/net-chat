using Microsoft.EntityFrameworkCore;
using OurSpace.API.Data;
using OurSpace.API.Hubs;
using OurSpace.API.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add MessageService to the dependency injection container
builder.Services.AddScoped<MessageService>();

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = builder.Configuration["Redis:ConnectionString"];
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Redis:ConnectionString is not configured.");
    }
    return ConnectionMultiplexer.Connect(connectionString);
});

builder.Services.AddScoped<RedisStreamProducerService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        policy => policy.WithOrigins("http://localhost:5500", "http://127.0.0.1:5500")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddSignalR()
    // Configure SignalR to use Redis as a backplane.
    .AddStackExchangeRedis(builder.Configuration["Redis:ConnectionString"]!, options => {
        options.Configuration.AbortOnConnectFail = false; // Don't abort if Redis isn't immediately available
        options.Configuration.SyncTimeout = 5000;
        options.Configuration.AsyncTimeout = 5000;
        options.Configuration.ConnectTimeout = 5000;
    });

builder.Services.AddControllers();

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("AllowSpecificOrigin");

app.UseStaticFiles();

app.MapControllers();

app.MapHub<ChatHub>("/chathub");

app.MapFallbackToFile("index.html");


var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
