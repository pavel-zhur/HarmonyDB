using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
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
    private readonly MessageMarkdownCombiner _messageMarkdownCombiner;

    public InlineQueryHandler(IOptions<TelegramOptions> telegramOptions, ILogger<InlineQueryHandler> logger, FullTextSearch fullTextSearch, SongsDatabase songsDatabase, MessageMarkdownCombiner messageMarkdownCombiner)
        : base(telegramOptions, songsDatabase)
    {
        _logger = logger;
        _fullTextSearch = fullTextSearch;
        _messageMarkdownCombiner = messageMarkdownCombiner;
    }

    protected override async Task<bool> HandleSync(Update update)
    {
        if (update.InlineQuery == null)
        {
            return false;
        }

        var offset = int.TryParse(update.InlineQuery.Offset, out var value) ? value : 0;

        SongsDatabase.Interactions.Add(new()
        {
            CreatedOn = DateTime.Now,
            UserId = update.InlineQuery.From.Id,
            InteractionType = InteractionType.InlineQuery,
            Serialized = JsonConvert.SerializeObject(update),
            ShortInfoSerialized = $"offset: {offset}; {update.InlineQuery.Query}",
        });
        await SongsDatabase.SaveChangesAsyncX();

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