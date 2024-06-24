using HarmonyDB.Index.Api.Services;
using HarmonyDB.Source.Api.Model;
using HarmonyDB.Source.Api.Model.V1.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.V1
{
    public class GetSong : AuthorizationFunctionBase<GetSongRequest, GetSongResponse>
    {
        private readonly CommonExecutions _commonExecutions;

        public GetSong(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, CommonExecutions commonExecutions, SecurityContext securityContext) 
            : base(loggerFactory, authorizationApiClient, securityContext)
        {
            _commonExecutions = commonExecutions;
        }

        [Function(SourceApiUrls.V1GetSong)]
        public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] GetSongRequest request)
            => RunHandler(req, request);

        protected override async Task<GetSongResponse> Execute(HttpRequest httpRequest, GetSongRequest request)
            => await _commonExecutions.GetSong(request);
    }
}
