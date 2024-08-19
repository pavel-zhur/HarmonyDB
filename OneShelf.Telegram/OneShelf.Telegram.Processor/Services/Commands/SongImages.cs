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
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using Constants = OneShelf.Telegram.Processor.Helpers.Constants;
using TelegramOptions = OneShelf.Telegram.Processor.Model.TelegramOptions;
using Version = OneShelf.Common.Database.Songs.Model.Version;

namespace OneShelf.Telegram.Processor.Services.Commands;

[BothCommand("song_images", "Картинки к песне")]
public class SongImages : Command
{
    private const int Limit = 42;

    private readonly ILogger<SongImages> _logger;
    private readonly MessageMarkdownCombiner _messageMarkdownCombiner;
    private readonly SongsDatabase _songsDatabase;
    private readonly IllustrationsApiClient _illustrationsApiClient;
    private readonly TelegramOptions _options;
    private readonly FullTextSearch _fullTextSearch;
    private readonly TelegramBotClient _botClient;

    public SongImages(ILogger<SongImages> logger, Io io, MessageMarkdownCombiner messageMarkdownCombiner,
        SongsDatabase songsDatabase, IllustrationsApiClient illustrationsApiClient, IOptions<TelegramOptions> options,
        FullTextSearch fullTextSearch)
        : base(io)
    {
        _logger = logger;
        _messageMarkdownCombiner = messageMarkdownCombiner;
        _songsDatabase = songsDatabase;
        _illustrationsApiClient = illustrationsApiClient;
        _options = options.Value;
        _fullTextSearch = fullTextSearch;
        _botClient = new(options.Value.Token);
    }

    private enum SongImagesChoice
    {
        [Display(Name = "v5 (ркмнд!)")]
        Look5,

        [Display(Name = "v4 (ркмнд!)")]
        Look4,

        [Display(Name = "v2")]
        Look2,

        [Display(Name = "v1")]
        Look,

        [Display(Name = "Что за v1 v2 v3 v4 v5?")]
        Help,

        [Display(Name = "Посмотреть исходные запросы")]
        SeePrompts,

        [Display(Name = "Посмотреть исходный текст")]
        SeeLyrics,

        [Display(Name = "Догенерировать еще!")]
        AddMore,
    }

    protected override async Task ExecuteQuickly()
    {
        var user = await _songsDatabase.Users
            .Where(x => x.TenantId == _options.TenantId)
            .SingleAsync(x => x.Id == Io.UserId);
        if (!user.IsAuthorizedToUseIllustrations)
        {
            Io.WriteLine("Только участники чата имеют доступ.");
            return;
        }

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

        var (current, limit) = await Current();

        Io.WriteLine("Песня выбрана.");
        Io.WriteLine();

        var action = Io.StrictChoice<SongImagesChoice>("Что хотите сделать?");

        while (action == SongImagesChoice.Help)
        {
            Io.WriteLine(@"Коротко: рекомендую V5, если мало - то V4.

Во всех случаях я прошу языковую модель сделать картинки такие, чтобы по ним потом можно было угадать песню, она знает про игру и старается.

- V1 делает всегда пять картинок отвечающие на пять конкретных вопросов, и картинки получаются слишком сфокусированы каждая на своём вопросе, и целиком не очень хорошо передают песню, хотя и хорошо описывают её с разных аспектов, но аспекты не к каждой песне подходят, чтобы выдать то, что поможет её вспомнить.
Эти пять вопросов такие:
-- 1. Мысль / идея песни,
-- 2. Сюжет / события,
-- 3. Настроение / эмоции / атмосфера,
-- 4. Персонажи / объекты,
-- 5. Всё это вместе взятое (поэтому там часто коллаж :) ).

- V2 свободен в выборе картинок и решает сам какие выдать, решая задачу игры, то есть старается показать то, что поможет песню угадать.
И плюс я явно прошу V2 писать картинкам детальные запросы, включая мысль/идею, сюжет/события, настроение/эмоции/атмосферу, персонажи/объекты.
Но он решает сам что куда как включать, стараясь сделать лучше для решения задачи.

- V3 то же что и V2, но без просьбы писать детально и включать м/и/с/с/н/э/а/п/о.
Он часто создаёт краткие описания без акцента на всех этих аспектах.

- V4 это V2 только я дополнительно её прошу:
-- фокусировать картинки на самых необычных или выделяющихся частях,
-- избегать того, чего нет в песне,
-- если она применяет метафоры, то детально их описывать, чтобы генератор картинок раскрыл их близко к атмосфере и объектам песни,
-- дополнительно включить одну картинку с главной идеей песни (это было самое полезное из V1),
-- дополнительно включить одну собирательную картинку (это тоже было полезно из V1).

- V5 это V4 только:
-- картинок будет три,
-- оно постарается показать песню с разных сторон,
-- и фокусироваться на том на чём фокусируется песня,
-- и без надписей должно получиться.

");

            action = Io.StrictChoice<SongImagesChoice>("Что хотите сделать?");
        }

        (action, var version) = action switch
        {
            SongImagesChoice.Look2 => (SongImagesChoice.Look, 2),
            SongImagesChoice.Look4 => (SongImagesChoice.Look, 4),
            SongImagesChoice.Look5 => (SongImagesChoice.Look, 5),
            _ => (action, 1),
        };

        string? alterationKey = null;

        if ((user.IsAuthorizedToUseIllustrationAlterationsPermanently || user.AuthorizedToUseIllustrationAlterationsTemporarilySince.HasValue)
            && action is SongImagesChoice.Look or SongImagesChoice.AddMore
            && (version == 5 || Io.IsAdmin))
        {
            Dictionary<string, string?> alterations = new()
            {
                { "Норма, без темы", null },
                { "Злость 😈", "evil-2023.12.19" },
                { "Грусть 😭", "despair-2023.12.19" },
                { "Драма 😱", "drama-2023.12.19" },
            };
            
            alterationKey = Io.StrictChoice("Тема 😈😭😱:", x => alterations[x], alterations.Keys);
        }

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

            if (indices.Count > limit - current)
            {
                Io.WriteLine("У вас уже нет лимита на сегодня. Можно только смотреть картинки для песен которые вы уже сгенерировали.");
                return;
            }
        }

        if (action != SongImagesChoice.Look && current >= limit)
        {
            Io.WriteLine("У вас уже нет лимита на сегодня. Можно только смотреть картинки для песен которые вы уже сгенерировали.");
            return;
        }

        if (action == SongImagesChoice.AddMore)
        {
            version = Io.SemiStrictChoice("Версия:", new[]
            {
                "1", "2", "3", "4", "5", 
            }).SelectSingle(x => x.strict ?? x.free).SelectSingle(int.Parse!);
        }

        Io.WriteLine("Генерирую, это займет около минуты...");

        Scheduled(Background(song, action, indices, current < limit, version, alterationKey));
    }

    private async Task Background(Song song, SongImagesChoice action, IReadOnlyList<(int i, int j)>? more,
        bool stillCreditAvailable, int customVersion, string? alterationKey)
    {
        try
        {
            string? chosenVersion = null;
            OneResponse? chosenResponse = null;
            AllResponse all = null!;
            foreach (var version in song.Versions)
            {
                all = await _illustrationsApiClient.Generate(version.Uri.ToString(), customVersion, Io.UserId, $"song index {song.Index}, v {customVersion}, generating more", more?.Select(x => new GenerationIndex(x.i, x.j)).ToList(), alterationKey);
                var generated = all.Responses.Values.Single();
                if (more?.Any() == true)
                {
                    await LogGenerated(more.Count, customVersion, version, alterationKey);
                }

                if (generated.Prompts.Any(x => x.Value.Any(x => x.Any())))
                {
                    var phantomVersion = alterationKey != null
                        ? generated.Prompts.Keys.Single(v =>
                            all.AlteredVersions.GetValueOrDefault(v)?.BaseVersion == customVersion &&
                            all.AlteredVersions.GetValueOrDefault(v)?.Key == alterationKey)
                        : customVersion;

                    if (action == SongImagesChoice.Look && !generated.ImageIds[phantomVersion].SelectMany(x => x).SelectMany(x => x).Any())
                    {
                        if (!stillCreditAvailable)
                        {
                            Io.WriteLine("У вас уже нет лимита на сегодня. Можно только смотреть картинки для песен которые вы уже сгенерировали.");
                            return;
                        }

                        var i = generated.Prompts[phantomVersion]
                            .WithIndices()
                            .Select(x => (x.i, x.x.Count, length: x.x.Sum(x => x.Length)))
                            .Where(x => x.Count > 0) // strictly any images
                            .OrderBy(x => x.Count > 7 ? 2 : 1) // <= 7 are strictly higher priority
                            .ThenBy(x => x.Count > 7 ? x.Count : Math.Abs(5 - x.Count)) // for > 7, the less, the better; for <= 7, the closer to 5, the better
                            .ThenBy(x => x.Count) // otherwise, the less, the better
                            .ThenByDescending(x => x.length) // otherwise, longer descriptions are better
                            .First()
                            .i;

                        all = await _illustrationsApiClient.Generate(
                            version.Uri.ToString(), 
                            customVersion,
                            Io.UserId,
                            $"song index {song.Index}, v {customVersion}, alteration {alterationKey}",
                            generated.Prompts[phantomVersion][i].Select((_, j) => new GenerationIndex(i, j)).ToList(),
                            alterationKey);
                        generated = all.Responses.Values.Single();
                        await LogGenerated(generated.ImageIds[phantomVersion].Sum(x => x.Sum(x => x.Count)), customVersion, version, alterationKey);
                    }

                    if (!generated.Prompts[phantomVersion].Any(x => x.Any())
                        || !generated.ImageIds[phantomVersion].Any(x => x.Any()))
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

            if (action == SongImagesChoice.SeePrompts)
            {
                foreach (var (v, prompts) in chosenResponse!.Prompts.Where(x => x.Key > 0))
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

            var images = chosenResponse!.ImageIds.Where(x => x.Key > 0).SelectMany(x => x.Value.SelectMany(x => x.SelectMany(x => x))).ToList();
            if (images.Any())
            {
                var imagesWithIndices = chosenResponse.ImageIds
                    .Where(x => x.Key > 0)
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
                            await _botClient.SendMediaGroupAsync(Io.UserId, chunk
                                .WithIndices()
                                .Select(x => new InputMediaPhoto(_illustrationsApiClient.GetGetImagePublicUrl(x.x.id)!)));

                            var caption = $"🧩🧩v{all.AlteredVersions.GetValueOrDefault(grouping.Key)?.SelectSingle(x => $"{x.BaseVersion} {all.Alterations[x.Key].Title}") ?? grouping.Key.ToString()}{Environment.NewLine}" + string.Join(" ", chunk.Select(x => x.label));
                            await _botClient.SendMessageAsync(Io.UserId, caption);
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

                await Current();
                Io.WriteLine("Если картинки плохо отражают песню, сбивают с толку, или вы ожидали чего-то увидеть и не увидели, напишите Паше. Он как раз пытается сделать лучше. :)");
            }
        }
        catch (Exception e)
        {
            Io.WriteLine("Случилась ошибка. :(");
            _logger.LogError(e, "Error in the song image continuation.");
        }
    }

    private async Task<(int current, int limit)> Current()
    {
        var since = DateTime.Now.AddDays(-1);
        var previous = await _songsDatabase.Interactions
            .Where(x => x.UserId == Io.UserId)
            .Where(x => x.CreatedOn > since)
            .Where(x => x.InteractionType == InteractionType.SongImages)
            .ToListAsync();

        var current = previous.Sum(x => int.Parse(x.ShortInfoSerialized!));

        var markdown = new Markdown();
        markdown.Append("У вас пока использовано ");
        markdown.Append($"{current}/{Limit}".Bold());
        markdown.AppendLine(" картинок.");
        markdown.Append($"Каждому доступно {Limit} картинки в сутки, это примерно 7-9 песен. Смелее :)");
        Io.WriteLine(markdown);
        Io.WriteLine();
        return (current, Limit);
    }

    private async Task LogGenerated(int count, int customVersion, Version version, string? alterationKey)
    {
        _songsDatabase.Interactions.Add(new()
        {
            CreatedOn = DateTime.Now,
            InteractionType = InteractionType.SongImages,
            Serialized = JsonConvert.SerializeObject(new
            {
                Count = count,
                AlterationKey = alterationKey,
                Version = customVersion,
                Url = version.Uri.ToString(),
            }),
            ShortInfoSerialized = count.ToString(),
            UserId = Io.UserId,
        });
        await _songsDatabase.SaveChangesAsyncX();
    }
}