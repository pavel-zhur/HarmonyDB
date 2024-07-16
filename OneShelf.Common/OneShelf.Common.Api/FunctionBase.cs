using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace OneShelf.Common.Api;

public abstract class FunctionBase<TRequest>
{
    private readonly ConcurrencyLimiter? _concurrencyLimiter;

    protected readonly ILogger Logger;

    protected FunctionBase(ILoggerFactory loggerFactory, ConcurrencyLimiter? concurrencyLimiter = null)
    {
        _concurrencyLimiter = concurrencyLimiter;
        Logger = loggerFactory.CreateLogger(GetType());
    }

    protected async Task<IActionResult> RunHandler(TRequest request)
    {
        try
        {
            Logger.LogInformation("C# HTTP trigger function processed a request.");
            OnBeforeExecution();

            if (_concurrencyLimiter != null)
            {
                return await _concurrencyLimiter.ExecuteOrThrow(() => ExecuteSuccessful(request));
            }
                
            return await ExecuteSuccessful(request);
        }
        catch (ServiceConcurrencyException e)
        {
            Logger.LogError(e, "Too many requests.");
            return new StatusCodeResult(429);
        }
        catch (ServiceCacheItemNotFoundException e)
        {
            Logger.LogWarning(e, "Cache item not found.");
            return new StatusCodeResult((int)ServiceCacheItemNotFoundException.StatusCode);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error executing the function.");
            return new StatusCodeResult(500);
        }
    }

    protected virtual void OnBeforeExecution()
    {
    }

    protected abstract Task<IActionResult> ExecuteSuccessful(TRequest request);
}

public abstract class FunctionBase<TRequest, TResponse> : FunctionBase<TRequest>
{
    protected FunctionBase(ILoggerFactory loggerFactory, ConcurrencyLimiter? concurrencyLimiter = null)
        : base(loggerFactory, concurrencyLimiter)
    {
    }

    protected override async Task<IActionResult> ExecuteSuccessful(TRequest request) => new OkObjectResult(await Execute(request));

    protected abstract Task<TResponse> Execute(TRequest request);
}