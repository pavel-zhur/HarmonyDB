using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace OneShelf.Common.Api;

internal class ErrorHandlingMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            try
            {
                context.GetLogger<ErrorHandlingMiddleware>().LogError(e, "Uncaught error in the function execution.");
                var httpResponseData = context.GetHttpResponseData() ?? (await context.GetHttpRequestDataAsync())!.CreateResponse();
                httpResponseData.StatusCode = HttpStatusCode.InternalServerError;
            }
            catch (Exception e2)
            {
                context.GetLogger<ErrorHandlingMiddleware>().LogError(e2, "An error in the error handler itself.");
            }
        }
    }
}