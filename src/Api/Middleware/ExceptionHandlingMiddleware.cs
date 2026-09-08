using System.Net;
using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IOptions<JsonOptions> jsonOptions)
    {
        _next = next;
        _logger = logger;
        // Reuse the same options MVC serializes normal responses with (camelCase),
        // otherwise this hand-rolled write falls back to JsonSerializer's PascalCase
        // default and error bodies stop matching every other endpoint's casing.
        _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (status, title, errors) = exception switch
        {
            ValidationAppException ex => (HttpStatusCode.BadRequest, ex.Message, (IDictionary<string, string[]>?)ex.Errors),
            NotFoundException ex => (HttpStatusCode.NotFound, ex.Message, null),
            ForbiddenException ex => (HttpStatusCode.Forbidden, ex.Message, null),
            ConflictException ex => (HttpStatusCode.Conflict, ex.Message, null),
            UnauthorizedAppException ex => (HttpStatusCode.Unauthorized, ex.Message, null),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.", null)
        };

        if (status == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;

        var response = new ApiErrorResponse
        {
            Title = title,
            Status = (int)status,
            Errors = errors
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, _jsonOptions));
    }
}
