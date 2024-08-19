namespace OneShelf.Telegram.Options;

public class TelegramTypes
{
    private readonly List<Type> _commands = new();
    
    private readonly List<Type> _pipelineHandlers = new();
    
    public IReadOnlyList<Type> Commands => _commands;

    public IReadOnlyList<Type> PipelineHandlers => _pipelineHandlers;

    internal List<Type> MutableCommands => _commands;

    internal List<Type> MutablePipelineHandlers => _pipelineHandlers;
}