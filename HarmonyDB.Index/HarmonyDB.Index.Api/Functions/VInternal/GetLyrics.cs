﻿using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VInternal;
using HarmonyDB.Index.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.VInternal;

public class GetLyrics : ServiceFunctionBase<GetLyricsRequest, GetLyricsResponse>
{
    private readonly CommonExecutions _commonExecutions;

    public GetLyrics(ILoggerFactory loggerFactory, CommonExecutions commonExecutions, SecurityContext securityContext)
        : base(loggerFactory, securityContext)
    {
        _commonExecutions = commonExecutions;
    }

    [Function(IndexApiUrls.VInternalGetLyrics)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] GetLyricsRequest request)
        => RunHandler(request);

    protected override async Task<GetLyricsResponse> Execute(GetLyricsRequest request)
    {
        var externalId = (await _commonExecutions.GetSourcesAndExternalIds(new Uri(request.Url).Once().ToList())).Attributes.Single().Value.ExternalId;

        var chords = (await _commonExecutions.GetSong(externalId)).Song;
        
        return new()
        {
            Lyrics = chords.Output.AsLyrics(),
        };
    }
}