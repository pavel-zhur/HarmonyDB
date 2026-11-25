using HarmonyDB.Shelf.Model;
using Microsoft.AspNetCore.Diagnostics;

namespace HarmonyDB.Shelf.Api.Foundation.Errors;

/// <summary>
/// Every error the API answers has the body <see cref="ErrorResponse"/>: from a <see cref="ShelfException"/>, from an
/// exception nobody caught (500, logged), and from an answer with an error status and no body (an unknown address, a
/// failed authorization …).
/// </summary>
internal static class ErrorResponses
{
    public static Task WriteAsync(HttpContext context, int statusCode, string code, IReadOnlyDictionary<string, string>? details = null)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new ErrorResponse { Code = code, Details = details }, context.RequestAborted);
    }

    /// <summary>
    /// The code of an answer that has only a status.
    /// </summary>
    public static string CodeOf(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => ErrorCodes.SignInRequired,
        StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status409Conflict => ErrorCodes.Conflict,
        StatusCodes.Status429TooManyRequests => ErrorCodes.TooManyRequests,
        StatusCodes.Status503ServiceUnavailable => ErrorCodes.Unavailable,
        >= 500 => ErrorCodes.InternalError,
        _ => ErrorCodes.BadRequest,
    };

    /// <summary>
    /// A middleware: answers a <see cref="ShelfException"/> and a malformed request with their codes. These are answers,
    /// not failures, and are not logged as errors.
    /// </summary>
    public static async Task AnswerExpectedErrorsAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (ShelfException exception) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            await WriteAsync(context, exception.StatusCode, exception.Code, exception.Details);
        }
        catch (BadHttpRequestException exception) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            await WriteAsync(context, exception.StatusCode, CodeOf(exception.StatusCode));
        }
    }

    /// <summary>
    /// The exception handler of the app, for what nobody expected (the exception handler middleware has logged it).
    /// </summary>
    public static Task HandleUnexpectedExceptionAsync(HttpContext context)
        => WriteAsync(context, StatusCodes.Status500InternalServerError, ErrorCodes.InternalError);

    /// <summary>
    /// Gives an error answer that has no body its code.
    /// </summary>
    public static Task HandleStatusCodeAsync(StatusCodeContext context)
    {
        var statusCode = context.HttpContext.Response.StatusCode;
        return WriteAsync(context.HttpContext, statusCode, CodeOf(statusCode));
    }
}
