using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Illustrations.Database;
using OneShelf.Illustrations.Database.Models;
using System.Drawing;
using System.Text.Json;
using Grpc.Net.Client.Configuration;
using HarmonyDB.Index.Api.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Common.OpenAi.Services;
using OneShelf.Illustrations.Api.Constants;
using OneShelf.Illustrations.Api.Services;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace OneShelf.Illustrations.Api.Functions
{
    public class Generate : FunctionBase<GenerateRequest, AllResponse?>
    {
        private readonly IllustrationsCosmosDatabase _illustrationsCosmosDatabase;
        private readonly IndexApiClient _indexApiClient;
        private readonly DialogRunner _dialogRunner;
        private readonly HttpClient _httpClient;
        private readonly ImagesProcessor _imagesProcessor;
        private readonly AllReader _allReader;

        public Generate(ILoggerFactory loggerFactory, IllustrationsCosmosDatabase illustrationsCosmosDatabase, DialogRunner dialogRunner, HttpClient httpClient, ImagesProcessor imagesProcessor, AllReader allReader, IndexApiClient indexApiClient)
           : base(loggerFactory)
        {
            _illustrationsCosmosDatabase = illustrationsCosmosDatabase;
            _dialogRunner = dialogRunner;
            _httpClient = httpClient;
            _imagesProcessor = imagesProcessor;
            _allReader = allReader;
            _indexApiClient = indexApiClient;
        }

        [Function(IllustrationsApiUrls.Generate)]
        public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req, [FromBody] GenerateRequest request) => RunHandler(request);

        protected override async Task<AllResponse?> Execute(GenerateRequest generateRequest)
        {
            if (generateRequest.SpecialSystemMessage.HasValue && !SystemMessages.SpecialSystemMessages.ContainsKey(generateRequest.SpecialSystemMessage.Value))
                throw new("Wrong special system message.");

            if (generateRequest.SpecialSystemMessage.HasValue == (generateRequest.CustomSystemMessage != null))
                throw new($"Exactly one of {nameof(generateRequest.SpecialSystemMessage)}, {nameof(generateRequest.CustomSystemMessage)} should be set.");

            if (generateRequest.SpecialSystemMessage == null && generateRequest.AlterationKey != null)
                throw new($"The {nameof(generateRequest.SpecialSystemMessage)} is required when the {nameof(generateRequest.AlterationKey)} is set.");

            var url = generateRequest.Url;
            var allIllustrationsPrompts = await _illustrationsCosmosDatabase.GetPrompts(url);
            var illustrationsPrompts = allIllustrationsPrompts.FirstOrDefault(x => x.SpecialSystemMessage == generateRequest.SpecialSystemMessage
                && x.CustomSystemMessage == generateRequest.CustomSystemMessage
                && x.AlterationKey == generateRequest.AlterationKey);

            if (illustrationsPrompts == null)
            {
                var lyrics = allIllustrationsPrompts.FirstOrDefault()?.Lyrics ?? await _indexApiClient.GetLyrics(url);
                if (string.IsNullOrWhiteSpace(lyrics))
                    return null;

                var message = generateRequest.CustomSystemMessage ??
                              SystemMessages.SpecialSystemMessages[generateRequest.SpecialSystemMessage!.Value];

                if (generateRequest.AlterationKey != null)
                    message = $"{message}\r\n{SystemMessages.Alterations[generateRequest.AlterationKey].systemMessage}";

                var (prompts, memoryPoint) = await _dialogRunner.GenerateSongImages(
                    lyrics, 
                    message,
                    generateRequest.UserId,
                    generateRequest.AdditionalBillingInfo);

                illustrationsPrompts = new()
                {
                    Id = Guid.NewGuid(),
                    CreatedOn = DateTime.Now,
                    Url = url,
                    Version = "2",
                    Lyrics = lyrics,
                    Prompts = prompts,
                    Trace = JsonConvert.DeserializeObject(JsonSerializer.Serialize(memoryPoint))!, // :)
                    SpecialSystemMessage = generateRequest.SpecialSystemMessage,
                    CustomSystemMessage = generateRequest.CustomSystemMessage,
                    AlterationKey = generateRequest.AlterationKey,
                };

                await _illustrationsCosmosDatabase.AddPrompts(illustrationsPrompts);
                allIllustrationsPrompts.Add(illustrationsPrompts);
            }

            if (generateRequest.GenerateIndices?.Any() == true)
            {
                var urls = await _dialogRunner.GenerateImages(
                    generateRequest.GenerateIndices
                        .Select(x => illustrationsPrompts.Prompts[x.AttemptIndex][x.PhotoIndex]).ToList(), new()
                    {
                        ImagesVersion = 3,
                        SystemMessage = null!,
                        Version = null!,
                        UserId = generateRequest.UserId,
                        AdditionalBillingInfo = generateRequest.AdditionalBillingInfo,
                        UseCase = "illustrations images",
                        DomainId = null,
                        });

                foreach (var (index, remoteUrl) in generateRequest.GenerateIndices.Zip(urls))
                {
                    if (remoteUrl != null)
                    {
                        var bytes = await _httpClient.GetByteArrayAsync(remoteUrl);
                        bytes = _imagesProcessor.ConvertToJpeg(bytes);
                        await _illustrationsCosmosDatabase.AddIllustration(new()
                        {
                            Id = Guid.NewGuid(),
                            CreatedOn = DateTime.Now,
                            Url = url,
                            Image = bytes,
                            Version = "2",
                            AttemptIndex = index.AttemptIndex,
                            PhotoIndex = index.PhotoIndex,
                            PromptsId = illustrationsPrompts.Id,
                        });
                    }
                }
            }

            return await _allReader.Read(generateRequest.Url);
        }
    }
}
