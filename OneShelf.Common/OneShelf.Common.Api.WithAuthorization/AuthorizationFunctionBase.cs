using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Authorization.Api.Model;

namespace OneShelf.Common.Api.WithAuthorization;

public abstract class AuthorizationFunctionBase<TRequest>
    where TRequest : IRequestWithIdentity
{
    private readonly ConcurrencyLimiter? _concurrencyLimiter;
    private readonly AuthorizationApiClient _authorizationApiClient;
    private readonly bool _respectServiceCode;
    
    protected readonly SecurityContext SecurityContext;
    protected readonly ILogger Logger;

    protected AuthorizationFunctionBase(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, SecurityContext securityContext, ConcurrencyLimiter? concurrencyLimiter = null, bool respectServiceCode = false, bool limitConcurrency = false)
    {
        _concurrencyLimiter = limitConcurrency ? concurrencyLimiter ?? throw new("Concurrency limiter is required.") : null;
        Logger = loggerFactory.CreateLogger(GetType());
        _authorizationApiClient = authorizationApiClient;
        SecurityContext = securityContext;
        _respectServiceCode = respectServiceCode;
    }

    protected async Task<IActionResult> RunHandler(HttpRequest httpRequest, TRequest request)
    {
        try
        {
            Logger.LogInformation("C# HTTP trigger function processed a request.");

            var authorizationCheckResult = _respectServiceCode ? await _authorizationApiClient.CheckIdentityRespectingCode(request.Identity) : await _authorizationApiClient.CheckIdentity(request.Identity);
            if (authorizationCheckResult == null)
            {
                SecurityContext.InitService();
                return await ExecuteWithConcurrency(httpRequest, request);
            }

            if (authorizationCheckResult.IsSuccess)
            {
                SecurityContext.InitSuccessful(request.Identity, authorizationCheckResult);
                return await ExecuteWithConcurrency(httpRequest, request);
            }

            return await ExecuteFailed(authorizationCheckResult.AuthorizationError!);
        }
        catch (UnauthorizedException e)
        {
            return new UnauthorizedObjectResult(e.Message);
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

    private async Task<IActionResult> ExecuteWithConcurrency(HttpRequest httpRequest, TRequest request)
    {
        if (_concurrencyLimiter != null)
        {
            return await _concurrencyLimiter.ExecuteOrThrow(() => ExecuteSuccessful(httpRequest, request));
        }

        return await ExecuteSuccessful(httpRequest, request);
    }

    protected abstract Task<IActionResult> ExecuteSuccessful(HttpRequest httpRequest, TRequest request);

    protected virtual Task<IActionResult> ExecuteFailed(string authorizationError)
        => Task.FromResult<IActionResult>(new UnauthorizedObjectResult(authorizationError));
}

public abstract class AuthorizationFunctionBase<TRequest, TResponse> : AuthorizationFunctionBase<TRequest>
    where TRequest : IRequestWithIdentity
{
    protected AuthorizationFunctionBase(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, SecurityContext securityContext, ConcurrencyLimiter? concurrencyLimiter = null, bool respectServiceCode = false, bool limitConcurrency = false)
        : base(loggerFactory, authorizationApiClient, securityContext, concurrencyLimiter, respectServiceCode, limitConcurrency)
    {
    }

    protected override async Task<IActionResult> ExecuteSuccessful(HttpRequest httpRequest, TRequest request)
        => new OkObjectResult(await Execute(httpRequest, request));

    protected abstract Task<TResponse> Execute(HttpRequest httpRequest, TRequest request);
}