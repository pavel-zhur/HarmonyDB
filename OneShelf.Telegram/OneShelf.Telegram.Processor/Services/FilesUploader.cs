using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace OneShelf.Telegram.Processor.Services;

public class FilesUploader
{
    private readonly ILogger<FilesUploader> _logger;
    private readonly ExponentialBackOff _exponentialBackOff;
    private readonly TelegramOptions _telegramOptions;

    public FilesUploader(ILogger<FilesUploader> logger, IOptions<TelegramOptions> telegramOptions, ExponentialBackOff exponentialBackOff)
    {
        _logger = logger;
        _exponentialBackOff = exponentialBackOff;
        _telegramOptions = telegramOptions.Value;
    }

    public async Task<string> Upload(Song song)
    {
        if (song.FileId != null) throw new ArgumentOutOfRangeException(nameof(song));
        if (song.Content == null) throw new ArgumentNullException(nameof(song.Content));

        var client = new TelegramBotClient(_telegramOptions.Token);

        return await _exponentialBackOff.WithRetry(async () =>
        {
            using var memoryStream = new MemoryStream(song.Content.Content);
            var m = await client.SendDocumentAsync(new(_telegramOptions.FilesChatId),
                new InputFileStream(memoryStream, song.GetFileName()),
                caption: song.GetCaption(withAdditionalKeywords: false),
                disableNotification: true);

            return m.Document!.FileId;
        });
    }
}