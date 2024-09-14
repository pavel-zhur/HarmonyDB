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
        result.AppendLine("Пишите мне — я отвечаю. Или попросите сделать картинку, отправьте команду или посмотрите помощь - /help. У меня ежедневный бесплатный лимит для всех.\n\n🐶");
        return result;
    }

    public Markdown GetHelpResponseHeader()
    {
        var result = new Markdown();
        result.AppendLine("Привет! Спасибо, что написали мне. Я — собака-чатбот (GPT-4o) 🐶. Могу вести текстовые диалоги и создавать изображения с помощью DALL-E 3 — самых свежих ИИ-моделей на сегодня (август 2024). Обожаю играть с текстами и картинками!");
        result.AppendLine();
        result.AppendLine("Каждому пользователю телеграма доступен небольшой бесплатный лимит: примерно 30 сообщений или 5 картинок в день. Использовать сервис сверх лимита пока нельзя, но мой программист работает, не покладая лап. Оплата будет возможна карточками из Украины, РФ, РБ и других стран примерно с 1 ноября. Связаться с разработчиком можно по адресу: @pavel_zhur.");
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