using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Processor.Model;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Processor.Services.Commands;

[BothCommand("friends_likes", "Шортлисты друзей")]
public class FriendsLikes : Command
{
    private readonly ILogger<FriendsLikes> _logger;
    private readonly MessageMarkdownCombiner _messageMarkdownCombiner;
    private readonly SongsDatabase _songsDatabase;
    private readonly TelegramOptions _options;

    public FriendsLikes(ILogger<FriendsLikes> logger, Io io, MessageMarkdownCombiner messageMarkdownCombiner, SongsDatabase songsDatabase, IOptions<TelegramOptions> options)
        : base(io)
    {
        _logger = logger;
        _messageMarkdownCombiner = messageMarkdownCombiner;
        _songsDatabase = songsDatabase;
        _options = options.Value;
    }

    protected override async Task ExecuteQuickly()
    {
        var current = await _songsDatabase.Users
            .SingleAsync(x => x.Id == Io.UserId);

        if (current.TenantId != _options.TenantId)
        {
            Io.WriteLine("Только для тех, кто в чате.");
            return;
        }

        var users = await _songsDatabase.Users.Where(x => x.Likes.Any()).OrderBy(x => x.Title).Where(x => x.Id != Io.UserId).ToListAsync();
        var labels = users.Select((x, i) => (x, label: $"{i + 1}. {x.Title}")).ToList();
        var user = Io.StrictChoice("Чей шортлист посмотрим?", x => labels.Single(y => y.label == x).x, labels.Select(x => x.label).ToList());

        var messages = await _messageMarkdownCombiner.Shortlists(user.Id);
        foreach (var (_, message, _, _) in messages)
        {
            Io.AdditionalOutput(message.BodyOrCaption);
        }
    }
}