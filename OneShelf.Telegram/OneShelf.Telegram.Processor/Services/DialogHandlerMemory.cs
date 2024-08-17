using System.Reflection;
using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.IoMemories;
using OneShelf.Telegram.Processor.Model.CommandAttributes;
using OneShelf.Telegram.Processor.Services.Commands;
using OneShelf.Telegram.Processor.Services.Commands.Admin;

namespace OneShelf.Telegram.Processor.Services;

public class DialogHandlerMemory
{
    private readonly ILogger<DialogHandlerMemory> _logger;
    private readonly Dictionary<long, IoMemory> _memory = new();
    private readonly List<List<(Type type, CommandAttribute attribute)>> _commands;

    public DialogHandlerMemory(ILogger<DialogHandlerMemory> logger)
    {
        _logger = logger;

        var commands = new List<List<Type>>
        {
            new()
            {
                typeof(Start),
            },
            new()
            {
                typeof(Help),
            },

            new()
            {
                typeof(SongImages),
            },

            new()
            {
                typeof(SongImagesTries),
            },

            new()
            {
                typeof(Likes),
            },
            new()
            {
                typeof(FriendsLikes),
            },
            new()
            {
                typeof(Search),
            },

            new()
            {
                typeof(ImproveWithParameters),
            },

            new()
            {
                typeof(Temp),
                typeof(Bowowow),
            },

            new()
            {
                typeof(AddSong),
            },

            new()
            {
                typeof(AdditionalKeywords),
                typeof(ChangeSongVersion),
                typeof(Rename),
            },

            new()
            {
                typeof(MergeSongs),
            },

            new()
            {
                typeof(ChangeSongCategories),
                typeof(ChangeSongIsLive),
            },

            new()
            {
                typeof(ChangeArtistCategories),
                typeof(UpdateArtistSynonyms),
                typeof(RenameArtist),
                typeof(SwapArtistAndSynonym)
            },

            new()
            {
                typeof(MergeArtists)
            },

            new()
            {
                typeof(QueueUpdateAll),
                typeof(MeasureAll),
                typeof(QueueDropLists),
            },

            new()
            {
                typeof(ConfigureChatGpt),
            },

            new()
            {
                typeof(AuthorizeWebForLastOwnChatter),
            },

            new()
            {
                typeof(ListMultiArtistSongs),
                typeof(ListSongsWithAdditionalKeywordsOrComments),
            },

            new()
            {
                typeof(ListDraft),
                typeof(ListLiveNoChords),
                typeof(ListPostponed),
                typeof(ListArchived),
            },

            new()
            {
                typeof(UploadReadyOnes),
                typeof(UploadReadyOnesAll),
            },

            new()
            {
                typeof(UpdateCommands),
            },

            new()
            {
                typeof(Inspiration)
            },

            new()
            {
                typeof(AskWeb),
            },

            new()
            {
                typeof(NeverPromoteTopics),
            },
        };

        _commands = commands.Select(x => x.Select(x => (x, attribute: x.GetCustomAttribute<CommandAttribute>())).ToList()).ToList();
    }

    public IEnumerable<(Type commandType, CommandAttribute attribute)> GetCommands(Role availableTo) => _commands
        .SelectMany(x => x).Where(x => availableTo >= x.attribute.Role);

    public IEnumerable<IEnumerable<CommandAttribute>> GetCommandsGrid(Role availableTo) => _commands
        .Select(x => x.Where(x => x.attribute.SupportsNoParameters).Where(x => availableTo >= x.attribute.Role)
            .Select(x => x.attribute).ToList())
        .Where(x => x.Any())
        .ToList();


    public (Type commandType, CommandAttribute attribute) Help => GetCommands(Role.Regular).Single(x => x.commandType == typeof(Help));
    public (Type commandType, CommandAttribute attribute) Search => GetCommands(Role.Regular).Single(x => x.commandType == typeof(Search));

    public IoMemory? Get(long userId)
    {
        lock (_memory)
        {
            return _memory.TryGetValue(userId, out var value) ? value : null;
        }
    }

    public void Set(long userId, IoMemory ioMemory)
    {
        lock (_memory)
        {
            _memory[userId] = ioMemory;
        }
    }

    public void Erase(long userId)
    {
        lock (_memory)
        {
            _memory.Remove(userId);
        }
    }
}