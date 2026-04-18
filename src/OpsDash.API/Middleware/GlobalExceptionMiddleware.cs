using System.Text.Json;
using FluentValidation;
using OpsDash.Application.DTOs.Common;

namespace OpsDash.API.Middleware;

public class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteErrorResponseAsync(context, ex);
        }
    }

    private async Task WriteErrorResponseAsync(HttpContext context, Exception ex)
    {
        context.Response.Clear();
        context.Response.ContentType = "application/json";

        var (statusCode, message, errors) = MapException(ex);
        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            message = _environment.IsDevelopment()
                ? ex.Message
                : "An internal server error occurred";
        }
        else if (_environment.IsDevelopment())
        {
            message = ex.Message;
        }

        context.Response.StatusCode = statusCode;

        var body = new ErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            Errors = errors,
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }

    private (int statusCode, string message, List<string>? errors) MapException(Exception ex)
    {
        return ex switch
        {
            UnauthorizedAccessException uax => (StatusCodes.Status401Unauthorized, uax.Message, null),
            KeyNotFoundException knf => (StatusCodes.Status404NotFound, knf.Message, null),
            ArgumentNullException anx => (StatusCodes.Status400BadRequest, anx.Message, null),
            ArgumentException ax => (StatusCodes.Status400BadRequest, ax.Message, null),
            ValidationException vx => (
                StatusCodes.Status400BadRequest,
                vx.Message,
                vx.Errors.Select(e => e.ErrorMessage).ToList()),
            _ => (StatusCodes.Status500InternalServerError, ex.Message, null),
        };
    }
}
