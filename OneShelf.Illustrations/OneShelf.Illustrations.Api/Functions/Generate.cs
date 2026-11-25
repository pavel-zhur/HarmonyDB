using Microsoft.Extensions.Logging;
using OneShelf.Illustrations.Api.Model;
using HarmonyDB.Index.Api.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneShelf.Common.Api;
using OneShelf.Illustrations.Api.Constants;
using OneShelf.Illustrations.Api.Services;

namespace OneShelf.Illustrations.Api.Functions
{
    public class Generate : FunctionBase<GenerateRequest, AllResponse?>
    {
        private readonly IndexApiClient _indexApiClient;
        private readonly IllustrationsGenerator _illustrationsGenerator;
        private readonly AllReader _allReader;

        public Generate(ILoggerFactory loggerFactory, IllustrationsGenerator illustrationsGenerator, AllReader allReader, IndexApiClient indexApiClient)
           : base(loggerFactory)
        {
            _illustrationsGenerator = illustrationsGenerator;
            _allReader = allReader;
            _indexApiClient = indexApiClient;
        }

        [Function(IllustrationsApiUrls.Generate, AuthorizationLevel.Function, "get", "post", InternalScope = IllustrationsModule.InternalScope)]
        public Task<IActionResult> Run(HttpRequest req, [FromBody] GenerateRequest request) => RunHandler(request);

        protected override async Task<AllResponse?> Execute(GenerateRequest generateRequest)
        {
            if (generateRequest.SpecialSystemMessage.HasValue && !SystemMessages.SpecialSystemMessages.ContainsKey(generateRequest.SpecialSystemMessage.Value))
                throw new("Wrong special system message.");

            if (generateRequest.SpecialSystemMessage.HasValue == (generateRequest.CustomSystemMessage != null))
                throw new($"Exactly one of {nameof(generateRequest.SpecialSystemMessage)}, {nameof(generateRequest.CustomSystemMessage)} should be set.");

            if (generateRequest.SpecialSystemMessage == null && generateRequest.AlterationKey != null)
                throw new($"The {nameof(generateRequest.SpecialSystemMessage)} is required when the {nameof(generateRequest.AlterationKey)} is set.");

            var url = generateRequest.Url;
            var illustrationsPrompts = await _illustrationsGenerator.GetOrCreatePrompts(
                url,
                () => _indexApiClient.GetLyrics(url),
                generateRequest.SpecialSystemMessage,
                generateRequest.CustomSystemMessage,
                generateRequest.AlterationKey,
                generateRequest.UserId,
                generateRequest.AdditionalBillingInfo);

            if (illustrationsPrompts == null)
                return null;

            if (generateRequest.GenerateIndices?.Any() == true)
            {
                await _illustrationsGenerator.GenerateImages(url, illustrationsPrompts, generateRequest.GenerateIndices, generateRequest.UserId, generateRequest.AdditionalBillingInfo);
            }

            return await _allReader.Read(generateRequest.Url);
        }
    }
}
