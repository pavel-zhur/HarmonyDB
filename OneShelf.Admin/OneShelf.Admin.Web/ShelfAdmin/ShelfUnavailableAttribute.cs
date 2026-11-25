using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace OneShelf.Admin.Web.ShelfAdmin;

/// <summary>
/// On the shelf's pages: when the shelf cannot be reached, or answers with an error the page does not expect, a plain
/// page says so instead of the error page.
/// </summary>
public sealed class ShelfUnavailableAttribute : ExceptionFilterAttribute
{
    private const string Unavailable = "The shelf does not answer right now. Try again in a minute.";

    public override void OnException(ExceptionContext context)
    {
        var (message, status) = context.Exception switch
        {
            HttpRequestException => (Unavailable, StatusCodes.Status503ServiceUnavailable),
            ShelfAdminException { Status: >= HttpStatusCode.InternalServerError } => (Unavailable, StatusCodes.Status503ServiceUnavailable),
            ShelfAdminException e => ($"The shelf refused the request ({(int)e.Status} {e.Code}).", StatusCodes.Status502BadGateway),
            _ => ((string?)null, 0),
        };
        if (message == null)
            return;

        context.Result = new ViewResult
        {
            ViewName = "Message",
            ViewData = new ViewDataDictionary(context.HttpContext.RequestServices.GetRequiredService<IModelMetadataProvider>(), context.ModelState)
            {
                Model = message,
            },
            StatusCode = status,
        };
        context.ExceptionHandled = true;
    }
}
