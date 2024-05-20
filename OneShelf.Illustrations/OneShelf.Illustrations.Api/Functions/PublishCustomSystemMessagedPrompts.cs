using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Api;
using OneShelf.Common.Cosmos;
using OneShelf.Illustrations.Api.Constants;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Illustrations.Api.Services;
using OneShelf.Illustrations.Database;

namespace OneShelf.Illustrations.Api.Functions;

public class PublishCustomSystemMessagedPrompts : FunctionBase<PublishCustomSystemMessagedPromptsRequest, PublishCustomSystemMessagedPromptsResponse>
{
    private readonly IllustrationsCosmosDatabase _illustrationsCosmosDatabase;

    public PublishCustomSystemMessagedPrompts(ILoggerFactory loggerFactory, IllustrationsCosmosDatabase illustrationsCosmosDatabase)
        : base(loggerFactory)
    {
        _illustrationsCosmosDatabase = illustrationsCosmosDatabase;
    }

    [Function(IllustrationsApiUrls.PublishCustomSystemMessagedPrompts)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req, [FromBody] PublishCustomSystemMessagedPromptsRequest request) => RunHandler(request);

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
            await _illustrationsCosmosDatabase.PublishCustomSystemMessage(request.IllustrationId,
                request.SpecialSystemMessage, request.AlterationKey);
        }
        catch (SupportedException e)
        {
            return new(e.Message);
        }

        return new("OK");
    }
}