namespace OneShelf.Telegram.Processor.Model;

public record TerminalAction
{
    public TerminalAction(string name, string alias, Action execution)
    {
        Name = name;
        Alias = alias;
        Execution = () =>
        {
            execution();
            return Task.FromResult(Task.CompletedTask);
        };
    }
    public TerminalAction(string name, string alias, Func<Task> execution)
    {
        Name = name;
        Alias = alias;
        Execution = async () =>
        {
            await execution();
            return Task.CompletedTask;
        };
    }

    public TerminalAction(string name, string alias, Func<Task<Task>> execution)
    {
        Name = name;
        Alias = alias;
        Execution = execution;
    }

    public string Name { get; }
    public string Alias { get; }
    public Func<Task<Task>> Execution { get; }
}