using Microsoft.AspNetCore.Mvc;
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
}