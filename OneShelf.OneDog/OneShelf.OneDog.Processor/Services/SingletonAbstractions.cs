using OneShelf.OneDog.Processor.Services.Commands;
using OneShelf.OneDog.Processor.Services.Commands.Admin;
using OneShelf.OneDog.Processor.Services.Commands.DomainAdmin;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDog.Processor.Services;

public class SingletonAbstractions : ISingletonAbstractions
{
    public List<List<Type>> GetCommandsGrid() =>
    [
        [
            typeof(Start)
        ],

        [
            typeof(Help),
            typeof(Nothing)
        ],


        [
            typeof(Temp)
        ],


        [
            typeof(ConfigureChatGpt),
            typeof(ConfigureDog),
            typeof(ViewBilling)
        ],


        [
            typeof(UpdateCommands)
        ]

    ];

    public Type GetDefaultCommand() => typeof(Nothing);

    public Type GetHelpCommand() => typeof(Help);
}