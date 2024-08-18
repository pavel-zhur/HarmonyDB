using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Pdfs.Generation.Inspiration.Models;
using OneShelf.Pdfs.Generation.Inspiration.Services;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.CommandAttributes;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Services.Base;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;
using Telegram.BotAPI.AvailableTypes;
using TelegramOptions = OneShelf.Telegram.Processor.Model.TelegramOptions;

namespace OneShelf.Telegram.Processor.Services.Commands.Admin;

[AdminCommand("a_insp", "Inspiration")]
public class Inspiration : Command
{
    private readonly ILogger<Inspiration> _logger;
    private readonly InspirationGeneration _inspirationGeneration;
    private readonly TelegramOptions _telegramOptions;

    public Inspiration(ILogger<Inspiration> logger, Io io, InspirationGeneration inspirationGeneration,
        IOptions<TelegramOptions> telegramOptions)
        : base(io)
    {
        _logger = logger;
        _inspirationGeneration = inspirationGeneration;
        _telegramOptions = telegramOptions.Value;
    }

    protected override async Task ExecuteQuickly()
    {
        var dataOrdering = Io.StrictChoice<InspirationDataOrdering>("Data ordering");
        var withChords = Io.StrictChoice<Confirmation>("With chords info?") == Confirmation.Yes;

        var compactArtists = dataOrdering is InspirationDataOrdering.ByArtist or InspirationDataOrdering.ByArtistWithSynonyms
                             && Io.StrictChoice<Confirmation>("Compact artists?") == Confirmation.Yes;

        var onlyPublished = Io.StrictChoice<Confirmation>("Only published?") == Confirmation.Yes;

        Scheduled(Background(dataOrdering, withChords, compactArtists, onlyPublished));
    }

    private async Task Background(InspirationDataOrdering dataOrdering, bool withChords, bool compactArtists,
        bool onlyPublished)
    {
        var pdfFile = await _inspirationGeneration.Inspiration(_telegramOptions.TenantId, dataOrdering, withChords, compactArtists, onlyPublished);
        var api = new TelegramBotClient(_telegramOptions.Token);
        await api.SendDocumentAsync(_telegramOptions.AdminId, new InputFile(pdfFile, $"Inspiration {dataOrdering}{(withChords ? " with chords" : null)}{(compactArtists ? " compact" : null)}.pdf"), caption: "Inspiration");
    }
}