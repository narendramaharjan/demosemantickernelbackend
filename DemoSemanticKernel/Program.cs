using DemoSemanticKernel.Services;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using DemoSemanticKernel.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Text to SQL API",
        Version = "v1",
        Description = "API for converting natural language to SQL queries",
        Contact = new OpenApiContact
        {
            Name = "Text to SQL Team",
            Email = "support@text-to-sql.com"
        }
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});

// Register services
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<ISqlGeneratorService, SqlGeneratorService>();
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    // Serve Swagger UI at application root (https://localhost:7297/)
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = string.Empty; // Serve swagger at root
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Text to SQL API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();