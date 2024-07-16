using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OneShelf.Common.Api.Common;

namespace HarmonyDB.Playground.Web.Controllers;

public class PlaygroundControllerBase : Controller
{
    protected IActionResult Concurrency()
    {
        var result = View("Concurrency");
        result.StatusCode = (int)ConcurrencyException.StatusCode;
        return result;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var action = context.ActionDescriptor.DisplayName;
        var ipAddress = context.HttpContext.Connection.RemoteIpAddress;

        var tooManyRequests = false;
        // calculate tooManyRequests with a static dictionary field.
        // too many requests should be true if the same ActionName is called from the same IpAddress more than 120 times a minute, or 720 times an hour, or 2400 times a day.

        if (tooManyRequests)
        {
            context.Result = Concurrency();
            return;
        }

        base.OnActionExecuting(context);
    }
}