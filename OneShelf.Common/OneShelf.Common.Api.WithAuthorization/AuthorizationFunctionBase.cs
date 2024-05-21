using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Authorization.Api.Model;

namespace OneShelf.Common.Api.WithAuthorization;

public abstract class AuthorizationFunctionBase<TRequest>
    where TRequest : IRequestWithIdentity
{
    private readonly AuthorizationApiClient _authorizationApiClient;

    private int? _tenantId;
    private bool? _arePdfsAllowed;

    protected readonly ILogger Logger;
    
    protected AuthorizationFunctionBase(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient)
    {
        Logger = loggerFactory.CreateLogger(GetType());
        _authorizationApiClient = authorizationApiClient;
    }

    protected int TenantId => _tenantId ?? throw new("Not authorized.");

    protected bool ArePdfsAllowed => _arePdfsAllowed ?? throw new("Not authorized.");

    protected async Task<IActionResult> RunHandler(HttpRequest httpRequest, TRequest request)
    {
        try
        {
            Logger.LogInformation("C# HTTP trigger function processed a request.");

            var authorizationCheckResult = await CheckAuthorization(request.Identity);
            _tenantId = authorizationCheckResult.TenantId;
            _arePdfsAllowed = authorizationCheckResult.ArePdfsAllowed;
            if (authorizationCheckResult.IsSuccess)
            {
                return await ExecuteSuccessful(httpRequest, request);
            }

            return await ExecuteFailed(authorizationCheckResult.AuthorizationError!);
        }
        catch (UnauthorizedException e)
        {
            return new UnauthorizedObjectResult(e.Message);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error executing the function.");
            return new StatusCodeResult(500);
        }
    }

    protected virtual async Task<CheckIdentityResponse> CheckAuthorization(Identity identity)
    {
        return await _authorizationApiClient.CheckIdentity(identity);
    }

    protected abstract Task<IActionResult> ExecuteSuccessful(HttpRequest httpRequest, TRequest request);

    protected virtual Task<IActionResult> ExecuteFailed(string authorizationError)
        => Task.FromResult<IActionResult>(new UnauthorizedObjectResult(authorizationError));
}

public abstract class AuthorizationFunctionBase<TRequest, TResponse> : AuthorizationFunctionBase<TRequest>
    where TRequest : IRequestWithIdentity
{
    protected AuthorizationFunctionBase(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient)
        : base(loggerFactory, authorizationApiClient)
    {
    }

    protected override async Task<IActionResult> ExecuteSuccessful(HttpRequest httpRequest, TRequest request)
        => new OkObjectResult(await Execute(httpRequest, request));

    protected abstract Task<TResponse> Execute(HttpRequest httpRequest, TRequest request);
}