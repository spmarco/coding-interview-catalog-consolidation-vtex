using CatalogConsolidation.Application.Exceptions;
using CatalogConsolidation.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace CatalogConsolidation.Api.ErrorHandling;

/// <summary>
/// Maps every exception that escapes the import flow to an RFC 7807 ProblemDetails response.
/// The message of an unexpected exception is never sent to the client (it could carry database
/// details); the known, deliberately raised ones are safe to show.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title, exposeMessage) = Map(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Request failed with {StatusCode}: {Title}", statusCode, title);
        }
        else
        {
            _logger.LogWarning("Request rejected with {StatusCode}: {Title} {Detail}", statusCode, title, exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = statusCode,
                Title = title,
                Detail = exposeMessage ? exception.Message : null
            }
        });
    }

    private static (int StatusCode, string Title, bool ExposeMessage) Map(Exception exception) => exception switch
    {
        DomainException => (StatusCodes.Status400BadRequest, "Invalid request.", true),
        InvalidImportFileException => (StatusCodes.Status400BadRequest, "Invalid import file.", true),
        BadHttpRequestException badRequest => (badRequest.StatusCode, "Invalid request.", true),
        CatalogBusyException => (StatusCodes.Status503ServiceUnavailable, "The catalog is busy with another import; please retry.", true),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", false)
    };
}
