using Microsoft.Extensions.Logging;

namespace OneShelf.Common.Api.WithAuthorization;

public abstract class AnonymousFunctionBase<TRequest> : FunctionBase<TRequest>
{
    private readonly SecurityContext _securityContext;

    protected AnonymousFunctionBase(ILoggerFactory loggerFactory, SecurityContext securityContext)
        : base(loggerFactory)
    {
        _securityContext = securityContext;
    }

    protected override void OnBeforeExecution()
    {
        base.OnBeforeExecution();
        _securityContext.InitAnonymous();
    }
}

public abstract class AnonymousFunctionBase<TRequest, TResponse> : FunctionBase<TRequest, TResponse>
{
    private readonly SecurityContext _securityContext;

    protected AnonymousFunctionBase(ILoggerFactory loggerFactory, SecurityContext securityContext) : base(loggerFactory)
    {
        _securityContext = securityContext;
    }

    protected override void OnBeforeExecution()
    {
        base.OnBeforeExecution();
        _securityContext.InitAnonymous();
    }
}