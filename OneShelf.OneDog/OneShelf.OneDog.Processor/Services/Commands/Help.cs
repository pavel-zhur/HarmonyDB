using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.OneDog.Database;
using OneShelf.OneDog.Processor.Helpers;
using OneShelf.OneDog.Processor.Model;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
using OneShelf.OneDog.Processor.Services.Commands.Base;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Model;

namespace OneShelf.OneDog.Processor.Services.Commands;

[Command("help", "Справка", Role.Regular, "Справка по всем командам")]
public class Help : Command
{
    private readonly ILogger<Help> _logger;
    private readonly DialogHandlerMemory _dialogHandlerMemory;
    private readonly DogDatabase _dogDatabase;
    private readonly TelegramOptions _telegramOptions;

    public Help(ILogger<Help> logger, Io io, DialogHandlerMemory dialogHandlerMemory, IOptions<TelegramOptions> telegramOptions, DogDatabase dogDatabase, ScopeAwareness scopeAwareness)
        : base(io, scopeAwareness)
    {
        _logger = logger;
        _dialogHandlerMemory = dialogHandlerMemory;
        _dogDatabase = dogDatabase;
        _telegramOptions = telegramOptions.Value;
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
            : ScopeAwareness.Domain.Administrators.Any(x => x.Id == Io.UserId)
                ? Role.DomainAdmin
                : Role.Regular;

        foreach (var command in _dialogHandlerMemory.GetCommands(role).Where(x => x.attribute.SupportsNoParameters))
        {
            Io.WriteLine($"/{command.attribute.Alias}: {command.attribute.HelpDescription}");
        }
    }
}