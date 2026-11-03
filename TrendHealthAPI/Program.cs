using System.Text.Json.Serialization;
using CosmicStoreAPI.Error;
using CosmicStoreAPI.Middleware;
using Data;
using Data.Classes;
using Data.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

const string CorsPolicyName = "TrendHealthAPI";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddControllers(options =>
    {
        // Validation errors are keyed by the JSON / query names (camelCase) instead of the C# property names.
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
    })
    .AddJsonOptions(options =>
    {
        // Enums go over the wire as names ("Open", "Daily") instead of numbers.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// ==========================================
// 1. DATABASE SETUP (SQLite file, created and migrated on startup)
// ==========================================
// appsettings.json is git-ignored, so fall back to a local file if it has no connection string.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = "Data Source=trendhealth.db";
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddSingleton(TimeProvider.System);

// ==========================================
// 2. REDIS & CACHING (opt-in: only registered when ConnectionStrings:RedisConnection is set)
// ==========================================
var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection");

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(c =>
    {
        var configuration = ConfigurationOptions.Parse(redisConnectionString, true);
        configuration.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(configuration);
    });

    builder.Services.AddSingleton<IDatabase>(sp =>
        sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

    builder.Services.AddStackExchangeRedisCache(options => {
        options.Configuration = redisConnectionString;
    });

    builder.Services.AddSingleton<ICacheService, CacheService>();
}

// ==========================================
// 3. CORS, VALIDATION & ERROR HANDLING
// ==========================================
builder.Services.AddCors(options =>{
    options.AddPolicy(CorsPolicyName, policy=>{
        policy.AllowAnyHeader().AllowAnyMethod()
            .WithOrigins("http://localhost:4200", "https://localhost:4200")
            .WithExposedHeaders("X-Pagination");
    });
});

builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = actionContext =>
    {
        var errors = actionContext.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                // System.Text.Json reports body errors with a JSON path such as "$.dueDate".
                e => e.Key.StartsWith("$.") ? e.Key[2..] : e.Key,
                e => e.Value!.Errors.Select(FriendlyMessage).Distinct().ToArray());

        // A body that fails to deserialize also produces "The request field is required." for the whole
        // parameter. If there's a more specific field error, that extra entry is just noise.
        var parameterNames = actionContext.ActionDescriptor.Parameters.Select(p => p.Name);
        if (errors.Count > 1)
        {
            foreach (var name in parameterNames)
            {
                errors.Remove(name);
            }
        }

        return new BadRequestObjectResult(new ApiValidationResponse(errors));
    };

    // JSON converter errors include .NET type names and byte offsets. Don't send those to clients.
    static string FriendlyMessage(Microsoft.AspNetCore.Mvc.ModelBinding.ModelError err) =>
        string.IsNullOrEmpty(err.ErrorMessage) || err.ErrorMessage.StartsWith("The JSON value could not be converted")
            ? "The value is not valid for this field."
            : err.ErrorMessage;
});

builder.Services.AddTransient<ExceptionMiddleware>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}

// Configure the HTTP request pipeline.
app.UseMiddleware<ExceptionMiddleware>();

app.UseCors(CorsPolicyName);

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
