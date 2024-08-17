using Microsoft.Extensions.Logging;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.Telegram.Services.Base;
using OneShelf.Telegram.Helpers;

namespace OneShelf.OneDog.Processor.Services.Commands;

[Command("help", "Справка", Role.Regular, "Справка по всем командам")]
public class Help : Command
{
    private readonly ILogger<Help> _logger;
    private readonly AvailableCommands _availableCommands;
    private readonly DogContext _dogContext;

    public Help(ILogger<Help> logger, Io io, AvailableCommands availableCommands, DogContext dogContext)
        : base(io)
    {
        _logger = logger;
        _availableCommands = availableCommands;
        _dogContext = dogContext;
    }

    protected override async Task ExecuteQuickly()
    {
        Io.WriteLine("Добрый день!".Bold());
        Io.WriteLine();
        Io.WriteLine("Это ИИ-бот, который использует GPT и DALL-E.");
        Io.WriteLine("Личность могут настраивать администраторы.");
        Io.WriteLine();
        Io.WriteLine("Для своей корректной работы, чтобы при ответах помнить контекст предыдущих примерно 20 сообщений, я сохраняю в базе данных сообщения той ветки, в которой я работаю. Только одной ветки только одного чата.");
        Io.WriteLine("Добавлять в другие чаты меня бесполезно, я не буду работать. Напишите @pavel_zhur, если хотите.");
        Io.WriteLine();
        Io.WriteLine("Наш с вами разговор (здесь и в чате) приватный (для вас и для участников чата), содержание разговора не будет нигде публиковаться.");
        Io.WriteLine("Картинки, которые я рисую, могут быть опубликованы где-нибудь (может быть планируется галерея), без упоминания текста диалогов, в которых они были нарисованы.");
        Io.WriteLine();
        Io.WriteLine("Для помощи, пишите @pavel_zhur.");
        Io.WriteLine();
        Io.WriteLine("Помимо этого, вот чем я могу помочь (почти ничем):");
        Io.WriteLine();

        var role = Io.IsAdmin
            ? Role.Admin
            : _dogContext.Domain.Administrators.Any(x => x.Id == Io.UserId)
                ? Role.DomainAdmin
                : Role.Regular;

        foreach (var command in _availableCommands.GetCommands(role).Where(x => x.attribute.SupportsNoParameters))
        {
            Io.WriteLine($"/{command.attribute.Alias}: {command.attribute.HelpDescription}");
        }
    }
}