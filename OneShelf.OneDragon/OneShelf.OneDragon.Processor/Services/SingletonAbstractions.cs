using OneShelf.OneDragon.Processor.Commands;
using OneShelf.Telegram.Commands;
using OneShelf.Telegram.Model;
using OneShelf.Telegram.Services.Base;

namespace OneShelf.OneDragon.Processor.Services;

public class SingletonAbstractions : ISingletonAbstractions
{
    public List<List<Type>> GetCommandsGrid() => [
        [
            typeof(Images),
            typeof(Videos),
            typeof(Songs),
        ],
        [
            typeof(Help),     
            typeof(ViewBilling),
            typeof(Amnesia)
        ],
        [
            typeof(Start),
        ],
        [
            typeof(UpdateCommands),
        ],
    ];

    public Type? GetDefaultCommand() => null;

    public Type GetHelpCommand() => typeof(Help);

    public Markdown GetStartResponse()
    {
        var result = new Markdown();
        result.AppendLine("Привет-привет! 🐾");
        result.AppendLine();
        result.AppendLine("Пишите мне — я отвечаю. Или попросите сделать картинку, видео, 30-секундную музыку, отправьте команду или посмотрите помощь - /help. У меня ежедневный бесплатный лимит для всех.\n\n🐶");
        return result;
    }

    public Markdown GetHelpResponseHeader()
    {
        var result = new Markdown();
        result.AppendLine("Привет! Спасибо, что написали мне. Я — собака-чатбот (o3) 🐶. Могу вести текстовые диалоги и создавать изображения с помощью DALL-E 3, видео с помощью Azure OpenAI Sora, и 30-секундную музыку с помощью Google Lyria — самых свежих ИИ-моделей на сегодня. Обожаю играть с текстами, картинками, видео и музыкой!");
        result.AppendLine();
        result.AppendLine("Каждому пользователю телеграма доступен небольшой бесплатный лимит: примерно 30 сообщений, 5 картинок, 5 штук 30-секундной музыки в день, и 2 видео в неделю. Использовать сервис сверх лимита пока нельзя, но напишите @pavel_zhur, если хотите.");
        result.AppendLine();
        result.AppendLine("Пишите мне — буду отвечать с хвостом вприпрыжку! Также можете попросить меня создать картинки прямо в диалоге. Пока что меня нельзя добавлять в чаты. Я хороший мальчик (девочка?), но пока только наедине. Как только появится биллинг, поддержка чатов, или что-то еще — я дам знать!");
        result.AppendLine();
        result.AppendLine("А вот все мои команды:");
        return result;
    }

    public string? DialogContinuation => null;

    public string CommandNotFound => "Такой команды нет. Посмотрите помощь - /help.";
    
    public string BackgroundErrors => "Произошли ошибки в фоне.";

    public string BackgroundOperationComplete => "Фоновая операция завершилась.";

    public string OperationError => "Извините, случилась ошибка при выполнении операции.";

    public string? NoOperationPlaceholder => null;

    public string MiddleCommandResponsePostfix => "(или /start чтобы вернуться в начало)";
}