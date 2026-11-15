using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.Api.Services;

namespace OneShelf.Frontend.Api.Functions.V3
{
    public class Illustrations : AuthorizationFunctionBase<IllustrationsRequest, IllustrationsResponse>
    {
        private readonly IllustrationsReader _illustrationsReader;

        public Illustrations(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, IllustrationsReader illustrationsReader, SecurityContext securityContext)
            : base(loggerFactory, authorizationApiClient, securityContext)
        {
            _illustrationsReader = illustrationsReader;
        }

        [Function(ApiUrls.V3Illustrations, AuthorizationLevel.Anonymous, "post")]
        public async Task<IActionResult> Run(HttpRequest req,
            [FromBody] IllustrationsRequest request)
            => await RunHandler(req, request);

        protected override Task<IllustrationsResponse> Execute(HttpRequest httpRequest, IllustrationsRequest request)
            => _illustrationsReader.Go(SecurityContext.TenantId, 
                request.AllVersions ? IllustrationsReader.Mode.AllVersionsOneTenantWithUnderGeneration : IllustrationsReader.Mode.CollapseSongsIncludeTitles, 
                request.Etag);
    }
}
