using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Model;
using OneShelf.Authorization.Api.Services;
using OneShelf.Common.Api;

namespace OneShelf.Authorization.Api.Functions
{
    public class CheckIdentity : FunctionBase<Identity, CheckIdentityResponse>
    {
        private readonly AuthorizationChecker _authorizationChecker;

        public CheckIdentity(ILoggerFactory loggerFactory, AuthorizationChecker authorizationChecker)
            : base(loggerFactory)
        {
            _authorizationChecker = authorizationChecker;
        }

        [Function(nameof(CheckIdentity))]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] Identity identity) => await RunHandler(identity);

        protected override async Task<CheckIdentityResponse> Execute(Identity request)
        {
            var (authorizationError, tenantId, arePdfsAllowed) = await _authorizationChecker.Check(request);

            return new()
            {
                AuthorizationError = authorizationError,
                IsSuccess = tenantId.HasValue,
                TenantId = tenantId,
                ArePdfsAllowed = arePdfsAllowed,
            };
        }
    }
}
