using HarmonyDB.Index.Api.Client;
using HarmonyDB.Source.Api.Model.V1.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common;
using OneShelf.Common.Database.Songs;
using OneShelf.Frontend.Api.Model.V3.Api;
using OneShelf.Frontend.SpecificModel;
using OneShelf.Common.Api.WithAuthorization;
using HarmonyDB.Source.Api.Client;

namespace OneShelf.Frontend.Api.Functions.V3
{
    public class Search : AuthorizationFunctionBase<SearchRequest, SearchResponse>
    {
        private readonly SourceApiClient _sourceApiClient;
        private readonly SongsDatabase _songsDatabase;

        public Search(ILoggerFactory loggerFactory, SourceApiClient sourceApiClient, AuthorizationApiClient authorizationApiClient, SongsDatabase songsDatabase)
            : base(loggerFactory, authorizationApiClient)
        {
            _sourceApiClient = sourceApiClient;
            _songsDatabase = songsDatabase;
        }

        [Function(ApiUrls.V3Search)]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req, [FromBody] SearchRequest request) => await RunHandler(req, request);

        protected override async Task<SearchResponse> Execute(HttpRequest httpRequest, SearchRequest request)
        {
            return TryGetTag(request.Query, out var tag)
                ? await GoLocal(request.Identity, tag)
                : await _sourceApiClient.V1Search(request);
        }

        private bool TryGetTag(string query, out int tag)
        {
            tag = 0;
            query = query.Replace(" ", "");
            if (query.Length != 6 || query.Any(c => !char.IsDigit(c))) return false;
            if (!int.TryParse(query, out tag)) return false;
            return true;
        }

        private async Task<SearchResponse> GoLocal(Identity identity, int tag)
        {
            var version = await _songsDatabase.Versions.Where(x => x.Song.TenantId == TenantId).Include(x => x.Song).ThenInclude(x => x.Artists).FirstOrDefaultAsync(x => x.CollectiveSearchTag == tag);
            if (version != null)
            {
                var attributes = await _sourceApiClient.V1GetSourcesAndExternalIds(identity, version.Uri.Once());
                if (attributes.Any())
                {
                    var attribute = attributes.Single().Value;

                    var specificAttributes = new Dictionary<string, dynamic>();
                    new FrontendAttributesV1
                    {
                        SourceColor = SourceColor.Success,
                        BadgeText = "очень точные",
                    }.ToDictionary(specificAttributes);

                    return new()
                    {
                        Headers = new()
                        {
                            new()
                            {
                                Title = version.Song.Title,
                                SourceUri = version.Uri,
                                Source = attribute.Source,
                                ExternalId = attribute.ExternalId,
                                Artists = version.Song.Artists.Select(x => x.Name).ToList(),
                                Rating = null,
                                Type = null,
                                IsSupported = true,
                                SpecificAttributes = specificAttributes,
                            }
                        }
                    };
                }
            }

            return new()
            {
                Headers = new(),
            };
        }
    }
}
