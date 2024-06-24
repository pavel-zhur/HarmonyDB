using HarmonyDB.Index.Api.Functions.V1;
using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VPublic;
using HarmonyDB.Index.Api.Services;
using HarmonyDB.Source.Api.Model.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Collectives.Api.Client;
using OneShelf.Collectives.Api.Model.V2.Sub;
using OneShelf.Collectives.Api.Model.VInternal;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;
using System.Security;
using SecurityContext = OneShelf.Common.Api.WithAuthorization.SecurityContext;

namespace HarmonyDB.Index.Api.Functions.VPublic;

public class GetPublic : AnonymousFunctionBase<GetPublicRequest, GetPublicResponse>
{
    private readonly CommonExecutions _commonExecutions;
    private readonly CollectivesApiClient _collectivesApiClient;

    public GetPublic(ILoggerFactory loggerFactory, CollectivesApiClient collectivesApiClient,
        CommonExecutions commonExecutions, SecurityContext securityContext)
        : base(loggerFactory, securityContext)
    {
        _collectivesApiClient = collectivesApiClient;
        _commonExecutions = commonExecutions;
    }

    [Function(nameof(IndexApiUrls.VPublicGetPublic))]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] GetPublicRequest request)
        => RunHandler(request);

    protected override async Task<GetPublicResponse> Execute(GetPublicRequest getPublicRequest)
    {
        Chords chords;
        try
        {
            var externalId = (await _commonExecutions.GetSourcesAndExternalIds(new Uri(getPublicRequest.Url).Once().ToList())).Attributes.Single().Value.ExternalId;

            chords = (await _commonExecutions.GetSong(externalId)).Song;

            if (!chords.IsPublic)
                return new()
                {
                    Error = "The chords could not be found."
                };
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error getting the chords for {uri}", getPublicRequest.Url);
            return new()
            {
                Error = "The chords could not be found."
            };
        }

        GetResponse collective;
        try
        {
            collective = await _collectivesApiClient.Get(new Uri(getPublicRequest.Url));
        }
        catch (Exception)
        {
            return new()
            {
                Error = "The chords could not be found."
            };
        }

        if (collective.Version.Uri.ToString() != getPublicRequest.Url)
        {
            if (collective.Version.Collective.Visibility == CollectiveVisibility.Private)
            {
                return new()
                {
                    Error = "The chords are no longer available.",
                };
            }

            return new()
            {
                Redirect = collective.Version.Uri.ToString(),
            };
        }

        return new()
        {
            Html = chords.Output.AsHtml(new(getPublicRequest.Transpose, getPublicRequest.Alteration)),
            Title = chords.Title,
            Artists = chords.Artists?.ToList() ?? new(),
        };
    }
}