using CatalogConsolidation.Api.ErrorHandling;
using CatalogConsolidation.Application.Exceptions;
using CatalogConsolidation.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatalogConsolidation.Tests.Api;

public class GlobalExceptionHandlerTests
{
    public static TheoryData<Exception, int> MappedExceptions => new()
    {
        { new InvalidProductException("Product name is required."), 400 },
        { new InvalidImportFileException("The file is not a valid JSON array of product entries."), 400 },
        { new BadHttpRequestException("Unsupported media type.", StatusCodes.Status415UnsupportedMediaType), 415 },
        { new CatalogBusyException("The catalog is locked by another import. Please retry.", new Exception("busy")), 503 },
        { new InvalidOperationException("something unexpected"), 500 }
    };

    [Theory]
    [MemberData(nameof(MappedExceptions))]
    public async Task Maps_each_exception_to_its_status_code(Exception exception, int expectedStatusCode)
    {
        var (handler, problemDetails, context) = Create();

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(expectedStatusCode, context.Response.StatusCode);
        Assert.Equal(expectedStatusCode, problemDetails.Written!.ProblemDetails.Status);
        Assert.False(string.IsNullOrWhiteSpace(problemDetails.Written.ProblemDetails.Title));
    }

    [Fact]
    public async Task Known_exceptions_show_their_message_as_the_detail()
    {
        var (handler, problemDetails, context) = Create();

        await handler.TryHandleAsync(context, new InvalidImportFileException("The 'file' part is required."), CancellationToken.None);

        Assert.Equal("The 'file' part is required.", problemDetails.Written!.ProblemDetails.Detail);
    }

    [Fact]
    public async Task An_unexpected_exception_never_leaks_its_message()
    {
        var (handler, problemDetails, context) = Create();

        await handler.TryHandleAsync(context, new InvalidOperationException("SQLite Error 1: 'no such table: Secret'"), CancellationToken.None);

        Assert.Null(problemDetails.Written!.ProblemDetails.Detail);
    }

    private static (GlobalExceptionHandler Handler, RecordingProblemDetailsService ProblemDetails, DefaultHttpContext Context) Create()
    {
        var problemDetails = new RecordingProblemDetailsService();
        var handler = new GlobalExceptionHandler(problemDetails, NullLogger<GlobalExceptionHandler>.Instance);
        return (handler, problemDetails, new DefaultHttpContext());
    }

    private sealed class RecordingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? Written { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Written = context;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Written = context;
            return ValueTask.FromResult(true);
        }
    }
}
