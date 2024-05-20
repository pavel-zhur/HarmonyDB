using System.Reflection;
using Microsoft.Extensions.Logging;
using OneShelf.OneDog.Processor.Model;
using OneShelf.OneDog.Processor.Model.IoMemories;
using OneShelf.OneDog.Processor.Services.Commands;
using OneShelf.OneDog.Processor.Services.Commands.Admin;
using OneShelf.OneDog.Processor.Services.Commands.DomainAdmin;

namespace OneShelf.OneDog.Processor.Services;

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
                typeof(Nothing),
            },

            new()
            {
                typeof(Temp),
            },

            new()
            {
                typeof(ConfigureChatGpt),
                typeof(ConfigureDog),
                typeof(ViewBilling),
            },

            new()
            {
                typeof(UpdateCommands),
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
    public (Type commandType, CommandAttribute attribute) Nothing => GetCommands(Role.Regular).Single(x => x.commandType == typeof(Nothing));

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