using OneShelf.Common;
using OneShelf.Illustrations.Api.Constants;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Illustrations.Database;

namespace OneShelf.Illustrations.Api.Services;

public class AllReader
{
    private readonly IllustrationsCosmosDatabase _illustrationsCosmosDatabase;

    public AllReader(IllustrationsCosmosDatabase illustrationsCosmosDatabase)
    {
        _illustrationsCosmosDatabase = illustrationsCosmosDatabase;
    }

    public async Task<AllResponse> Read(string? url = null)
    {
        var allIllustrationsPrompts = (await _illustrationsCosmosDatabase.GetPrompts(url)).ToLookup(x => x.Url);
        var allIllustrations = (await _illustrationsCosmosDatabase.GetIllustrationHeaders(url)).ToLookup(x => x.Url);

        Dictionary<int, AlteredVersion> alterations = new();
        Dictionary<(int specialSystemMessage, string alterationKey), int> alterationsBack = new();

        return new()
        {
            SystemMessages = SystemMessages.SpecialSystemMessages,
            Alterations = SystemMessages.Alterations.ToDictionary(
                x => x.Key,
                x => new AvailableAlteration
                {
                    Title = x.Value.title,
                    SystemMessage = x.Value.systemMessage,
                }),
            AlteredVersions = alterations,
            Responses = allIllustrationsPrompts.Select(x => x.Key).Union(allIllustrations.Select(x => x.Key))
                .ToDictionary(
                    x => x,
                    x =>
                    {
                        var illustrationsPrompts = allIllustrationsPrompts[x].ToList();

                        var customVersions = illustrationsPrompts
                            .Where(x => x.CustomSystemMessage != null)
                            .OrderBy(x => x.CreatedOn)
                            .Select(x => x.CustomSystemMessage)
                            .WithIndices()
                            .ToDictionary(x => x.x!, x => -x.i - 1);

                        var promptsById = illustrationsPrompts
                            .GroupBy(x => (x.SpecialSystemMessage ?? customVersions[x.CustomSystemMessage!], x.AlterationKey))
                            .Select(g => g.OrderBy(x => x.CreatedOn).First())
                            .ToDictionary(
                                x => x.Id,
                                x =>
                                {
                                    var version = x.SpecialSystemMessage ?? customVersions[x.CustomSystemMessage!];

                                    if (x.AlterationKey != null)
                                    {
                                        var key = (x.SpecialSystemMessage.Value, x.AlterationKey);
                                        if (alterationsBack.TryGetValue(key, out version))
                                        {
                                        }
                                        else
                                        {
                                            version = (alterationsBack.Values.Max(x => (int?)x) ?? 1000) + 1;
                                            alterations[version] = new()
                                            {
                                                BaseVersion = x.SpecialSystemMessage.Value,
                                                Key = x.AlterationKey,
                                            };
                                            alterationsBack[key] = version;
                                        }
                                    }

                                    return (
                                        x,
                                        v: version,
                                        images: x.Prompts.Select(x => x.Select(_ => new List<Guid>()).ToList()).ToList(),
                                        publicUrls: x.Prompts
                                            .Select(x => x.Select(_ => new List<ImagePublicUrl>()).ToList())
                                            .ToList());
                                });

                        var headers = allIllustrations[x];
                        foreach (var header in headers.OrderBy(x => x.CreatedOn))
                        {
                            promptsById[header.PromptsId].images[header.AttemptIndex][header.PhotoIndex].Add(header.Id);
                            promptsById[header.PromptsId].publicUrls[header.AttemptIndex][header.PhotoIndex].Add(new()
                            {
                                Id = header.Id,
                                Url1024 = header.PublicUrl1024,
                                Url128 = header.PublicUrl128,
                                Url256 = header.PublicUrl256,
                                Url512 = header.PublicUrl512,
                            });
                        }

                        var allPrompts = promptsById.Values.OrderBy(x => x.v).ToList();

                        return new OneResponse
                        {
                            ImageIds = allPrompts.ToDictionary(x => x.v, x => x.images),
                            ImagePublicUrls = allPrompts.ToDictionary(x => x.v, x => x.publicUrls),
                            LyricsTrace = illustrationsPrompts.First().Lyrics,
                            Prompts = allPrompts.ToDictionary(x => x.v, x => x.x.Prompts),
                            EarliestCreatedOn = allPrompts.Min(x => x.x.CreatedOn),
                            LatestCreatedOn = allPrompts.Max(x => x.x.CreatedOn),
                            CustomSystemMessages = customVersions.ToDictionary(x => x.Value, x => x.Key),
                        };
                    }),
        };
    }
}