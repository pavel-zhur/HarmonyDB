using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Pdfs.Generation.Inspiration.Models;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Model.Ios;
using OneShelf.Telegram.Processor.Services.Commands.Base;
using Version = OneShelf.Common.Database.Songs.Model.Version;

namespace OneShelf.Telegram.Processor.Services.Commands;

[Command(Constants.ImproveCommandName, true, false, "Улучшить песню", "Улучшить песню", true, true)]
public class ImproveWithParameters : Command
{
    private readonly ILogger<ImproveWithParameters> _logger;
    private readonly SongsDatabase _songsDatabase;
    private readonly MessageMarkdownCombiner _messageMarkdownCombiner;
    private readonly RegenerationQueue _regenerationQueue;
    private readonly TelegramOptions _telegramOptions;

    public ImproveWithParameters(Io io, ILogger<ImproveWithParameters> logger, SongsDatabase songsDatabase,
        IOptions<TelegramOptions> telegramOptions, MessageMarkdownCombiner messageMarkdownCombiner,
        RegenerationQueue regenerationQueue, IOptions<TelegramOptions> options)
        : base(io, options)
    {
        _logger = logger;
        _songsDatabase = songsDatabase;
        _messageMarkdownCombiner = messageMarkdownCombiner;
        _regenerationQueue = regenerationQueue;
        _telegramOptions = telegramOptions.Value;
    }

    protected override async Task ExecuteQuickly()
    {
        if (!int.TryParse(Io.Parameters, out var index))
        {
            throw new($"The command parameters could not be parsed: {Io.Parameters}");
        }

        await Go(index);
    }

    public async Task Go(int index)
    {
        var song = await _songsDatabase.Songs
            .Where(x => x.TenantId == Options.TenantId)
            .Include(x => x.Artists)
            .Include(x => x.Versions)
            .ThenInclude(x => x.User)
            .Include(x => x.CreatedByUser)
            .Include(x => x.Messages)
            .SingleOrDefaultAsync(x => x.Index == index && x.Status == SongStatus.Live);
        if (song == null)
        {
            Io.WriteLine("Извините, такая песня почему-то не найдена.");
            return;
        }
        
        Io.WriteLine("Есть такая песенка.");
        WriteSongInfo(song);

        var count = song.Versions.Count(x => x.UserId == Io.UserId);
        // var isCurrentUser = song.CreatedByUserId == Io.UserId && !song.IsCommitted;


    restart: 
        var choice = Io.StrictChoice<ActionType>("Что делаем?", "Пожалуйста, выберите действие из предложенных.", x => x switch
        {
            ActionType.AddVersion => count < 2,
            ActionType.RemoveVersion => count > 0,
            _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
        });

        switch (choice)
        {
            case ActionType.AddVersion:
                Uri chordsLink;
                while (true)
                {
                    var entry = Io.FreeChoice("Введите ссылку на аккорды (только одну ссылку, пожалуйста):");
                    try
                    {
                        chordsLink = new(entry);
                        break;
                    }
                    catch
                    {
                        Io.WriteLine("Вы прислали не только ссылку, или не ссылку, или несколько ссылок.");
                    }
                }

                if (Io.StrictChoice<Confirmation>(
                        $"Точно хотите добавить аккорды {chordsLink}?") ==
                    Confirmation.No)
                    goto restart;

                song.Versions.Add(new()
                {
                    Uri = chordsLink,
                    UserId = Io.UserId,
                });
                await _songsDatabase.SaveChangesAsyncX(true);

                break;
            case ActionType.RemoveVersion:
                Version version;
                var versions = song.Versions.OrderBy(x => x.Id).Where(x => x.UserId == Io.UserId).ToList();
                if (versions.Count > 1)
                {
                    version = versions[
                        Io.StrictChoice("Которые из ваших аккордов удалить?",
                            x => int.Parse(x.Substring(0, x.IndexOf('.'))) - 1,
                            versions.Select((x, i) => $"{i + 1}. {x.Uri.Host}").ToList())];
                }
                else
                {
                    version = versions.Single();
                }

                if (version.PublishedSettings != null)
                {
                    Io.WriteLine("Не получится удалить, эти аккорды опубликованы.");
                    return;
                }

                if (Io.StrictChoice<Confirmation>(
                        $"Точно хотите удалить аккорды {version.Uri}?") ==
                    Confirmation.No)
                    goto restart;

                song.Versions.Remove(version);
                await _songsDatabase.SaveChangesAsyncX(true);

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        _regenerationQueue.QueueUpdateAll(false);

        Io.WriteLine($"{Constants.IconCheckMark} Сохранено!");

        WriteSongInfo(song);
    }

    private void WriteSongInfo(Song song)
    {
        var builder = new Markdown();
        builder.Append("Номер: ");
        builder.AppendLine(song.Index.ToString().Bold());

        if (song.CreatedByUserId != _telegramOptions.AdminId)
        {
            builder.Append("Добавлена: ");
            builder.AppendLine(song.CreatedByUserId.BuildUrl(song.CreatedByUser.Title.Bold()));
        }

        builder.Append("Название: ");
        builder.AppendLine(song.Title.Bold());
        foreach (var artist in song.Artists)
        {
            builder.Append("Автор: ");
            builder.AppendLine(artist.Name.Bold());
        }

        if (song.AdditionalKeywords != null)
        {
            builder.Append("Поисковая фраза: ");
            builder.AppendLine(song.AdditionalKeywords.Bold());
        }

        _messageMarkdownCombiner.AppendSongChordsLines(builder, song, true, false);
        Io.WriteLine(builder);
    }

    private enum ActionType
    {
        [Display(Name = "Добавить аккорды")]
        AddVersion,

        [Display(Name = "Удалить мои аккорды")]
        RemoveVersion,
    }
}