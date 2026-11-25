using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Api;
using OneShelf.Illustrations.Api.Constants;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Illustrations.Api.Services;
using OneShelf.Illustrations.Database;

namespace OneShelf.Illustrations.Api.Functions;

public class PublishCustomSystemMessagedPrompts : FunctionBase<PublishCustomSystemMessagedPromptsRequest, PublishCustomSystemMessagedPromptsResponse>
{
    private readonly IllustrationsDatabase _illustrationsDatabase;

    public PublishCustomSystemMessagedPrompts(ILoggerFactory loggerFactory, IllustrationsDatabase illustrationsDatabase)
        : base(loggerFactory)
    {
        _illustrationsDatabase = illustrationsDatabase;
    }

    [Function(IllustrationsApiUrls.PublishCustomSystemMessagedPrompts, AuthorizationLevel.Function, "get", "post", InternalScope = IllustrationsModule.InternalScope)]
    public Task<IActionResult> Run(HttpRequest req, [FromBody] PublishCustomSystemMessagedPromptsRequest request) => RunHandler(request);

    protected override async Task<PublishCustomSystemMessagedPromptsResponse> Execute(
        PublishCustomSystemMessagedPromptsRequest request)
    {
        if (request.AlterationKey != null && !SystemMessages.Alterations.ContainsKey(request.AlterationKey))
        {
            return new(
                $"Such alteration key is not found. Available: {string.Join(", ", SystemMessages.Alterations.Keys)}.");
        }

        try
        {
            await _illustrationsDatabase.PublishCustomSystemMessage(request.IllustrationId,
                request.SpecialSystemMessage, request.AlterationKey);
        }
        catch (SupportedException e)
        {
            return new(e.Message);
        }

        return new("OK");
    }
}