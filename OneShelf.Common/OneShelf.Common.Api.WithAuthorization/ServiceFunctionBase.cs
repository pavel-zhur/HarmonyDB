using Microsoft.Extensions.Logging;

namespace OneShelf.Common.Api.WithAuthorization;

public abstract class ServiceFunctionBase<TRequest> : FunctionBase<TRequest>
{
    private readonly SecurityContext _securityContext;

    protected ServiceFunctionBase(ILoggerFactory loggerFactory, SecurityContext securityContext,
        ConcurrencyLimiter? concurrencyLimiter = null)
        : base(loggerFactory, concurrencyLimiter)
    {
        _securityContext = securityContext;
    }

    protected override void OnBeforeExecution()
    {
        base.OnBeforeExecution();
        _securityContext.InitService();
    }
}

public abstract class ServiceFunctionBase<TRequest, TResponse> : FunctionBase<TRequest, TResponse>
{
    private readonly SecurityContext _securityContext;

    protected ServiceFunctionBase(ILoggerFactory loggerFactory, SecurityContext securityContext,
        ConcurrencyLimiter? concurrencyLimiter = null) 
        : base(loggerFactory, concurrencyLimiter)
    {
        _securityContext = securityContext;
    }

    protected override void OnBeforeExecution()
    {
        base.OnBeforeExecution();
        _securityContext.InitService();
    }
}