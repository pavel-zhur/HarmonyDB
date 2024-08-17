using System.Reflection;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Services;

public class AvailableCommands
{
    private readonly ISingletonAbstractions _singletonAbstractions;
    private readonly List<List<(Type type, CommandAttribute attribute)>> _commands;

    public AvailableCommands(ISingletonAbstractions singletonAbstractions)
    {
        _singletonAbstractions = singletonAbstractions;
        _commands = singletonAbstractions.GetCommandsGrid().Select(x => x.Select(x => (x, attribute: x.GetCustomAttribute<CommandAttribute>())).ToList()).ToList();
    }

    public IEnumerable<(Type commandType, CommandAttribute attribute)> GetCommands(Role availableTo) => _commands
        .SelectMany(x => x).Where(x => availableTo >= x.attribute.Role);

    public IEnumerable<IEnumerable<CommandAttribute>> GetCommandsGrid(Role availableTo) => _commands
        .Select(x => x.Where(x => x.attribute.SupportsNoParameters).Where(x => availableTo >= x.attribute.Role)
            .Select(x => x.attribute).ToList())
        .Where(x => x.Any())
        .ToList();


    public (Type commandType, CommandAttribute attribute) Help => GetCommands(Role.Regular).Single(x => x.commandType == _singletonAbstractions.GetHelpCommand());
    public (Type commandType, CommandAttribute attribute) Default => GetCommands(Role.Regular).Single(x => x.commandType == _singletonAbstractions.GetDefaultCommand());
}