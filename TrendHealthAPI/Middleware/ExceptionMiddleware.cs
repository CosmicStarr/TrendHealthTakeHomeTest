
using System.Net;
using System.Text.Json;
using CosmicStoreAPI.Error;
using Microsoft.AspNetCore.Mvc;

namespace CosmicStoreAPI.Middleware;

public class ExceptionMiddleware(IHostEnvironment environment, ILogger<ExceptionMiddleware> logger) : IMiddleware
{
    private readonly ILogger<ExceptionMiddleware> _logger = logger;
    private readonly IHostEnvironment _env = environment;

    private readonly string Info = "An unexpected error occurred on the server.";
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch(Exception ex)
        {
            await HandleExceptions(context,ex);
        }
    }

    private async Task HandleExceptions(HttpContext context, Exception ex)
    {
        
                _logger.LogError(ex, ex.Message);
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                var response = _env.IsDevelopment()
                    ? new ApiExceptionResponse((int)HttpStatusCode.InternalServerError, ex.Message, ex.StackTrace?.ToString())
                    : new ApiExceptionResponse((int)HttpStatusCode.InternalServerError,Info);

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var json = JsonSerializer.Serialize(response,options);
                await context.Response.WriteAsync(json);
    }
}