using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace OneShelf.Common.Api;

public abstract class FunctionBase<TRequest>
{
    protected readonly ILogger Logger;

    protected FunctionBase(ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger(GetType());
    }

    protected async Task<IActionResult> RunHandler(TRequest request)
    {
        try
        {
            Logger.LogInformation("C# HTTP trigger function processed a request.");

            return await ExecuteSuccessful(request);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error executing the function.");
            return new StatusCodeResult(500);
        }
    }

    protected abstract Task<IActionResult> ExecuteSuccessful(TRequest request);
}

public abstract class FunctionBase<TRequest, TResponse> : FunctionBase<TRequest>
{
    protected FunctionBase(ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
    }

    protected override async Task<IActionResult> ExecuteSuccessful(TRequest request) => new OkObjectResult(await Execute(request));

    protected abstract Task<TResponse> Execute(TRequest request);
}