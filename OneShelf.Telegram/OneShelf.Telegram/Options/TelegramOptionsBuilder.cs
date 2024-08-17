using OneShelf.Telegram.Services.Base;

namespace OneShelf.Telegram.Options;

public class TelegramOptionsBuilder
{
    private readonly TelegramTypes _types;

    public TelegramOptionsBuilder(TelegramTypes types)
    {
        _types = types;
    }

    public TelegramOptionsBuilder AddCommand<TCommand>()
        where TCommand : Command
    {
        _types.MutableCommands.Add(typeof(TCommand));
        return this;
    }

    public TelegramOptionsBuilder AddPipelineHandler<TPipelineHandler>()
        where TPipelineHandler : PipelineHandler
    {
        _types.MutablePipelineHandlers.Add(typeof(TPipelineHandler));
        return this;
    }
}