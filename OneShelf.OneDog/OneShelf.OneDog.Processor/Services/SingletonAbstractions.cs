using OneShelf.OneDog.Processor.Services.Commands;
using OneShelf.OneDog.Processor.Services.Commands.Admin;
using OneShelf.OneDog.Processor.Services.Commands.DomainAdmin;
using OneShelf.Telegram.Commands;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Model.Ios;
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

    public Markdown GetStartResponse()
    {
        var result = new Markdown();
        result.AppendLine("Добрый день!".Bold());
        result.AppendLine();
        result.AppendLine("Моя польза только в том чате, в котором я.");
        result.AppendLine("Тут ничего особого нет пока, можете настроить мою личность если вы администратор, можете посмотреть помощь - /help.");
        return result;
    }

    public Markdown GetHelpResponseHeader()
    {
        var result = new Markdown();
        result.AppendLine("Добрый день!".Bold());
        result.AppendLine();
        result.AppendLine("Это ИИ-бот, который использует GPT и DALL-E.");
        result.AppendLine("Личность могут настраивать администраторы.");
        result.AppendLine();
        result.AppendLine("Для своей корректной работы, чтобы при ответах помнить контекст предыдущих примерно 20 сообщений, я сохраняю в базе данных сообщения той ветки, в которой я работаю. Только одной ветки только одного чата.");
        result.AppendLine("Добавлять в другие чаты меня бесполезно, я не буду работать. Напишите @pavel_zhur, если хотите.");
        result.AppendLine();
        result.AppendLine("Наш с вами разговор (здесь и в чате) приватный (для вас и для участников чата), содержание разговора не будет нигде публиковаться.");
        result.AppendLine("Картинки, которые я рисую, могут быть опубликованы где-нибудь (может быть планируется галерея), без упоминания текста диалогов, в которых они были нарисованы.");
        result.AppendLine();
        result.AppendLine("Для помощи, пишите @pavel_zhur.");
        result.AppendLine();
        result.AppendLine("Помимо этого, вот чем я могу помочь (почти ничем):");
        return result;
    }
}