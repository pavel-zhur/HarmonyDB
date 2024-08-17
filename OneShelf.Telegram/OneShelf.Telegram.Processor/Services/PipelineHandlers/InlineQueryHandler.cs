using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Services.PipelineHandlers.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.GettingUpdates;
using Telegram.BotAPI.InlineMode;

namespace OneShelf.Telegram.Processor.Services.PipelineHandlers;

public class InlineQueryHandler : PipelineHandler
{
    private const int ResultsPerAnswer = 40;

    private readonly ILogger<InlineQueryHandler> _logger;
    private readonly FullTextSearch _fullTextSearch;
    private readonly SongsDatabase _songsDatabase;
    private readonly MessageMarkdownCombiner _messageMarkdownCombiner;

    public InlineQueryHandler(IOptions<TelegramOptions> telegramOptions, ILogger<InlineQueryHandler> logger, FullTextSearch fullTextSearch, SongsDatabase songsDatabase, MessageMarkdownCombiner messageMarkdownCombiner)
        : base(telegramOptions)
    {
        _logger = logger;
        _fullTextSearch = fullTextSearch;
        _songsDatabase = songsDatabase;
        _messageMarkdownCombiner = messageMarkdownCombiner;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (update.InlineQuery == null)
        {
            return false;
        }

        var offset = int.TryParse(update.InlineQuery.Offset, out var value) ? value : 0;

        _songsDatabase.Interactions.Add(new()
        {
            CreatedOn = DateTime.Now,
            UserId = update.InlineQuery.From.Id,
            InteractionType = InteractionType.InlineQuery,
            Serialized = JsonSerializer.Serialize(update),
            ShortInfoSerialized = $"offset: {offset}; {update.InlineQuery.Query}",
        });
        await _songsDatabase.SaveChangesAsyncX();

        var (found, isPersonal, version) = await _fullTextSearch.Find(update.InlineQuery.Query, update.InlineQuery.From.Id);

        var answerInlineQueryArgs = new AnswerInlineQueryArgs(
            update.InlineQuery.Id, 
            found
                .Skip(offset)
                .Take(10)
                .Select(song => _fullTextSearch.GetArticle(song, version, () => _messageMarkdownCombiner.Article(song))))
        {
            NextOffset = found.Count <= ResultsPerAnswer ? null : (offset + ResultsPerAnswer).ToString(),
            IsPersonal = isPersonal
        };

        QueueApi(update.InlineQuery.From.Id.ToString(), async api =>
        {
            try
            {
                await api.AnswerInlineQueryAsync(answerInlineQueryArgs);
            }
            catch (BotRequestException e) when (e.Message.Contains("query is too old"))
            {
            }
        });

        return true;
    }
}