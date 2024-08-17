using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OneShelf.Common;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Illustrations.Api.Client;
using OneShelf.Illustrations.Api.Model;
using OneShelf.Pdfs.Generation.Inspiration.Models;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Services.Commands.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Version = OneShelf.Common.Database.Songs.Model.Version;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("song_images_tries", "Картинки к песне попытки")]
public class SongImagesTries : Command
{
    private readonly ILogger<SongImagesTries> _logger;
    private readonly MessageMarkdownCombiner _messageMarkdownCombiner;
    private readonly SongsDatabase _songsDatabase;
    private readonly IllustrationsApiClient _illustrationsApiClient;
    private readonly FullTextSearch _fullTextSearch;
    private readonly TelegramBotClient _botClient;

    public SongImagesTries(ILogger<SongImagesTries> logger, Io io, MessageMarkdownCombiner messageMarkdownCombiner,
        SongsDatabase songsDatabase, IllustrationsApiClient illustrationsApiClient, IOptions<TelegramOptions> options,
        FullTextSearch fullTextSearch)
        : base(io, options)
    {
        _logger = logger;
        _messageMarkdownCombiner = messageMarkdownCombiner;
        _songsDatabase = songsDatabase;
        _illustrationsApiClient = illustrationsApiClient;
        _fullTextSearch = fullTextSearch;
        _botClient = new(options.Value.Token);
    }

    private enum SongImagesChoice
    {
        [Display(Name = "Сгенерить")]
        Look,

        [Display(Name = "Посмотреть исходные запросы")]
        SeePrompts,

        [Display(Name = "Посмотреть кастомные версии")]
        SeeCustomVersions,

        [Display(Name = "Посмотреть исходный текст")]
        SeeLyrics,

        [Display(Name = "Догенерировать еще!")]
        AddMore,
    }

    protected override async Task ExecuteQuickly()
    {
        var user = await _songsDatabase.Users
            .Where(x => x.TenantId == Options.TenantId)
            .SingleAsync(x => x.Id == Io.UserId);

        Song? song = null;
        while (true)
        {
            var query = Io.FreeChoice("Введите часть названия / номер / что-нибудь:");

            var found = (await _fullTextSearch.Find(query, Io.UserId)).found;

            Io.WriteLine($"{Constants.IconList} Результаты поиска {Constants.IconList}".Bold());
            Io.WriteLine();

            switch (found.Count)
            {
                case 0:
                    Io.WriteLine("Не нашлось ни одной песни.");
                    Io.WriteLine();
                    continue;
                case > 5:
                    Io.WriteLine($"Нашлось {found.Count} {found.Count.SongsPluralWord()}.{(found.Count > 5 ? " Показываем первые 5." : null)}".Bold());
                    Io.WriteLine();
                    break;
                case 1:
                    song = found.Single();
                    break;
            }

            Io.Write(await _messageMarkdownCombiner.SearchResult(found.Take(5).Select(x => x.Id).ToList()));
            if (song != null)
            {
                break;
            }

            Io.WriteLine("Уточните выбор песни.".Bold());
        }

        Io.WriteLine("Песня выбрана.");
        Io.WriteLine();

        var action = Io.StrictChoice<SongImagesChoice>("Что хотите сделать?");

        var customSystemMessage = Io.FreeChoice("Кастомное сообщение:");

        IReadOnlyList<(int i, int j)>? indices = null;
        if (action == SongImagesChoice.AddMore)
        {
            var more = Io.FreeChoice("С этим тут сложновато пока. Спросите у Паши что сюда писать.");
            var parsed = more.Split(new [] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(l => l.Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(l => l.Length == 2 && int.TryParse(l[0], out var i) && int.TryParse(l[1], out var j) ? (i - 1, j - 1) : ((int i, int j)?)null)
                .ToList();

            if (parsed.Any() && parsed.All(x => x.HasValue) || parsed.Count > 5)
            {
                indices = parsed.Select(x => x.Value).ToList();
            }
            else
            {
                Io.WriteLine("Не всё так просто. Спросите у Паши что сюда писать.");
                return;
            }
        }

        Io.WriteLine("Генерирую, это займет около минуты...");

        Scheduled(Background(song, action, indices, customSystemMessage));
    }

    private async Task Background(Song song, SongImagesChoice action, IReadOnlyList<(int i, int j)>? more, string customSystemMessage)
    {
        try
        {
            string? chosenVersion = null;
            OneResponse? chosenResponse = null;
            AllResponse all = null!;
            foreach (var version in song.Versions)
            {
                all = await _illustrationsApiClient.Generate(version.Uri.ToString(), customSystemMessage, Io.UserId, $"tries, song index {song.Index}, generating more", more?.Select(x => new GenerationIndex(x.i, x.j)).ToList());
                var generated = all.Responses.Values.Single();
                if (more?.Any() == true)
                {
                    await LogGenerated(more.Count, generated, version, customSystemMessage);
                }

                if (generated.Prompts.Any(x => x.Value.Any(x => x.Any())))
                {
                    var customVersion = generated.CustomSystemMessages.Single(x => x.Value == customSystemMessage).Key;

                    if (action == SongImagesChoice.Look && !generated.ImageIds[customVersion].SelectMany(x => x).SelectMany(x => x).Any())
                    {
                        all = await _illustrationsApiClient.Generate(version.Uri.ToString(), customSystemMessage, Io.UserId, $"tries, song index {song.Index}, v {customVersion}", generated.Prompts[customVersion].First().Select((_, j) => new GenerationIndex(0, j)).ToList());
                        generated = all.Responses.Values.Single();
                        await LogGenerated(generated.ImageIds[customVersion].Sum(x => x.Sum(x => x.Count)), generated, version, customSystemMessage);
                    }

                    if (!generated.Prompts[customVersion].Any(x => x.Any())
                        || !generated.ImageIds[customVersion].Any(x => x.Any()))
                    {
                        Io.WriteLine("Что-то пошло не так. Возможно имеет смысл попробовать еще раз для этой песни, либо если после повтора тоже не работает, значит надо чинить.");
                        return;
                    }

                    (chosenVersion, chosenResponse) = (version.Uri.ToString(), generated);
                    break;
                }
            }

            if (chosenVersion == null)
            {
                Io.WriteLine("Подходящих аккордов не нашлось в библиотеке.");
                return;
            }

            if (action == SongImagesChoice.SeeLyrics)
            {
                await _botClient.SendMessageAsync(Io.UserId, chosenResponse!.LyricsTrace);
                return;
            }

            if (action == SongImagesChoice.SeeCustomVersions)
            {
                foreach (var (v, message) in chosenResponse!.CustomSystemMessages)
                {
                    await _botClient.SendMessageAsync(Io.UserId, $"v{v}:");
                    await _botClient.SendMessageAsync(Io.UserId, message);
                }

                return;
            }

            if (action == SongImagesChoice.SeePrompts)
            {
                foreach (var (v, prompts) in chosenResponse!.Prompts)
                {
                    var builder = new StringBuilder();
                    builder.AppendLine($"v{v}");

                    for (var i = 0; i < prompts.Count; i++)
                    {
                        for (var j = 0; j < prompts[i].Count; j++)
                        {
                            builder.AppendLine($"{i + 1} {j + 1}: {prompts[i][j]}");
                        }

                        builder.AppendLine();
                    }

                    await _botClient.SendMessageAsync(Io.UserId, builder.ToString());
                }

                return;
            }

            var images = chosenResponse!.ImageIds.SelectMany(x => x.Value.SelectMany(x => x.SelectMany(x => x))).ToList();
            if (images.Any())
            {
                var imagesWithIndices = chosenResponse.ImageIds
                    .SelectMany(p => p.Value.SelectMany((x, i) =>
                        x.SelectMany((y, j) => y.Select((z, k) => (i, j, k, z, v: p.Key)))))
                    .OrderBy(x => x.v)
                    .ThenBy(x => x.i)
                    .ThenBy(x => x.j)
                    .ThenBy(x => x.k)
                    .Select(x => (id: x.z, label: $"🧩\u00A0v{all.AlteredVersions.GetValueOrDefault(x.v)?.BaseVersion ?? x.v} i{x.i + 1} j{x.j + 1} #{x.k}", x.v))
                    .ToList();

                if (_illustrationsApiClient.GetGetImagePublicUrl(Guid.NewGuid()) != null)
                {
                    foreach (var grouping in imagesWithIndices.GroupBy(x => x.v).OrderBy(x => x.Key))
                    {
                        foreach (var chunk in grouping.Chunk(10))
                        {
                            var caption = $"🧩🧩v{all.AlteredVersions.GetValueOrDefault(grouping.Key)?.SelectSingle(x => $"{x.BaseVersion} {all.Alterations[x.Key].Title}") ?? grouping.Key.ToString()}{Environment.NewLine}" + string.Join(" ", chunk.Select(x => x.label));
                            
                            await _botClient.SendMediaGroupAsync(Io.UserId, chunk
                                .WithIndices()
                                .Select(x => new InputMediaPhoto(_illustrationsApiClient.GetGetImagePublicUrl(x.x.id)!)
                                {
                                    Caption = x.i == 0 ? caption : null
                                }));
                        }
                    }
                }
                else
                {
                    var bytesTasks = images.Select(_illustrationsApiClient.GetImage).ToList();
                    await Task.WhenAll(bytesTasks);

                    var downloaded = bytesTasks
                        .Select(x => x.Result)
                        .Zip(imagesWithIndices)
                        .ToList();

                    foreach (var image in downloaded)
                    {
                        await _botClient.SendPhotoAsync(Io.UserId, new InputFile(image.First, $"{image.Second.id}.png"),
                            caption: image.Second.label);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Io.WriteLine("Случилась ошибка. :(");
            _logger.LogError(e, "Error in the song image continuation.");
        }
    }

    private async Task LogGenerated(int count, OneResponse generated, Version version, string customSystemMessage)
    {
        _songsDatabase.Interactions.Add(new()
        {
            CreatedOn = DateTime.Now,
            InteractionType = InteractionType.SongImages,
            Serialized = JsonConvert.SerializeObject(new
            {
                CustomSystemMessage = customSystemMessage,
                Count = count,
                Ids = generated.ImageIds,
                Prompts = generated.Prompts.First(),
                Url = version.Uri.ToString(),
            }),
            ShortInfoSerialized = count.ToString(),
            UserId = Io.UserId,
        });
        await _songsDatabase.SaveChangesAsyncX();
    }
}