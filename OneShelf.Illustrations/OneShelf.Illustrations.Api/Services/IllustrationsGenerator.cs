using System.Text.Json;
using Newtonsoft.Json;
using OneShelf.Common.OpenAi.Models;
using OneShelf.Common.OpenAi.Services;
using OneShelf.Illustrations.Api.Constants;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Illustrations.Database;
using OneShelf.Illustrations.Database.Models;
using OneShelf.Illustrations.Database.Services;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace OneShelf.Illustrations.Api.Services;

/// <summary>
/// Makes the prompts of illustrations for the lyrics of a song, and the images by them, and keeps both.
/// </summary>
public class IllustrationsGenerator(IllustrationsDatabase illustrationsDatabase, DialogRunner dialogRunner, ImagesProcessor imagesProcessor)
{
    /// <summary>
    /// The prompts kept for the address with the system message and the alteration, made when there are none yet: by
    /// the lyrics kept with the address's other prompts, else by <paramref name="lyrics"/>.
    /// </summary>
    /// <returns>Null when there are no lyrics.</returns>
    public async Task<IllustrationsPrompts?> GetOrCreatePrompts(
        string url, Func<Task<string?>> lyrics, int? specialSystemMessage, string? customSystemMessage, string? alterationKey, long? userId, string? additionalBillingInfo)
    {
        var allIllustrationsPrompts = await illustrationsDatabase.GetPrompts(url);
        var illustrationsPrompts = allIllustrationsPrompts.FirstOrDefault(x => x.SpecialSystemMessage == specialSystemMessage
            && x.CustomSystemMessage == customSystemMessage
            && x.AlterationKey == alterationKey);

        if (illustrationsPrompts != null)
            return illustrationsPrompts;

        var text = allIllustrationsPrompts.FirstOrDefault()?.Lyrics ?? await lyrics();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var message = customSystemMessage ?? SystemMessages.SpecialSystemMessages[specialSystemMessage!.Value];

        if (alterationKey != null)
            message = $"{message}\r\n{SystemMessages.Alterations[alterationKey].systemMessage}";

        var (prompts, memoryPoint) = await dialogRunner.GenerateSongImages(
            text,
            message,
            userId,
            additionalBillingInfo);

        illustrationsPrompts = new()
        {
            Id = Guid.NewGuid(),
            CreatedOn = DateTime.Now,
            Url = url,
            Version = "2",
            Lyrics = text,
            Prompts = prompts,
            Trace = JsonConvert.DeserializeObject(JsonSerializer.Serialize(memoryPoint))!, // :)
            SpecialSystemMessage = specialSystemMessage,
            CustomSystemMessage = customSystemMessage,
            AlterationKey = alterationKey,
        };

        await illustrationsDatabase.AddPrompts(illustrationsPrompts);
        return illustrationsPrompts;
    }

    /// <summary>
    /// Makes the images of the prompts at the indices, at once, and keeps each as soon as it is drawn: a batch cut short
    /// keeps what was drawn (and paid for).
    /// </summary>
    /// <returns>How many images were made.</returns>
    public async Task<int> GenerateImages(string url, IllustrationsPrompts illustrationsPrompts, IReadOnlyList<GenerationIndex> indices, long? userId, string? additionalBillingInfo)
    {
        var configuration = new DialogConfiguration
        {
            ImagesVersion = 3,
            SystemMessage = null!,
            Version = null!,
            UserId = userId,
            AdditionalBillingInfo = additionalBillingInfo,
            UseCase = "illustrations images",
            DomainId = null,
            ChatId = null,
        };

        // the database takes one at a time
        using var saving = new SemaphoreSlim(1);
        var made = 0;
        await Task.WhenAll(indices.Select(async index =>
        {
            var image = await dialogRunner.GenerateSongImageFile(illustrationsPrompts.Prompts[index.AttemptIndex][index.PhotoIndex], configuration);
            if (image == null)
                return;

            await saving.WaitAsync();
            try
            {
                await illustrationsDatabase.AddIllustration(new()
                {
                    Id = Guid.NewGuid(),
                    CreatedOn = DateTime.Now,
                    Url = url,
                    Image = imagesProcessor.ConvertToJpeg(image),
                    Version = "2",
                    AttemptIndex = index.AttemptIndex,
                    PhotoIndex = index.PhotoIndex,
                    PromptsId = illustrationsPrompts.Id,
                });
                made++;
            }
            finally
            {
                saving.Release();
            }
        }));

        return made;
    }
}