using System.Reflection;
using OneShelf.OneDog.Processor.Services.Commands;
using OneShelf.OneDog.Processor.Services.Commands.Admin;
using OneShelf.OneDog.Processor.Services.Commands.DomainAdmin;
using OneShelf.Telegram.Model;

namespace OneShelf.OneDog.Processor.Services;

public class AvailableCommands
{
    private readonly List<List<(Type type, CommandAttribute attribute)>> _commands;

    public AvailableCommands()
    {
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
}