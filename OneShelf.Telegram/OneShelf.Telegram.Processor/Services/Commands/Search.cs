using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Services.Base;
using Constants = OneShelf.Telegram.Processor.Helpers.Constants;

namespace OneShelf.Telegram.Processor.Services.Commands;

[BothCommand("search", "Поиск песен")]
public class Search : Command
{
    private readonly ILogger<Search> _logger;
    private readonly MessageMarkdownCombiner _messageMarkdownCombiner;
    private readonly FullTextSearch _fullTextSearch;

    public Search(ILogger<Search> logger, Io io, MessageMarkdownCombiner messageMarkdownCombiner,
        FullTextSearch fullTextSearch)
        : base(io)
    {
        _logger = logger;
        _messageMarkdownCombiner = messageMarkdownCombiner;
        _fullTextSearch = fullTextSearch;
    }

    protected override async Task ExecuteQuickly()
    {
        var query = Io.FreeChoice("Введите часть названия:");

        var found = (await _fullTextSearch.Find(query, Io.UserId)).found;

        Io.WriteLine($"{Constants.IconList} Результаты поиска {Constants.IconList}".Bold());
        Io.WriteLine();

        if (found.Any())
        {
            Io.WriteLine($"Нашлось {found.Count} {found.Count.SongsPluralWord()}.{(found.Count > 5 ? " Показываем первые 5." : null)}".Bold());
            Io.WriteLine();
        }

        Io.Write(await _messageMarkdownCombiner.SearchResult(found.Take(5).Select(x => x.Id).ToList()));
    }
}