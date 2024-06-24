using HarmonyDB.Index.Api.Functions.V1;
using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VInternal;
using HarmonyDB.Index.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;
using System.Security;
using SecurityContext = OneShelf.Common.Api.WithAuthorization.SecurityContext;

namespace HarmonyDB.Index.Api.Functions.VInternal;

public class TryImport : ServiceFunctionBase<TryImportRequest, TryImportResponse>
{
    private readonly CommonExecutions _commonExecutions;

    public TryImport(ILoggerFactory loggerFactory, CommonExecutions commonExecutions, SecurityContext securityContext)
        : base(loggerFactory, securityContext)
    {
        _commonExecutions = commonExecutions;
    }

    [Function(IndexApiUrls.VInternalTryImport)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] TryImportRequest request)
        => RunHandler(request);

    protected override async Task<TryImportResponse> Execute(TryImportRequest request)
    {
        try
        {
            var externalId = (await _commonExecutions.GetSourcesAndExternalIds(new Uri(request.Url).Once().ToList())).Attributes.Single().Value.ExternalId;

            var chords = (await _commonExecutions.GetSong(externalId)).Song;

            if (chords.Artists?.Count(x => !string.IsNullOrWhiteSpace(x)) is (null or 0) || string.IsNullOrWhiteSpace(chords.Title))
            {
                throw new("Artist or title are empty.");
            }

            var artist = string.Join(", ", chords.Artists.Where(x => !string.IsNullOrWhiteSpace(x)));

            return new()
            {
                Data = new()
                {
                    Source = chords.Source,
                    Artist = artist,
                    Title = chords.Title,
                },
            };
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Could not import the url: {url}", request.Url);
            return new()
            {
                Data = null,
            };
        }
    }
}