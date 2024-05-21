namespace OneShelf.Telegram.Processor.Model.CommandAttributes;

public class BothCommandAttribute : CommandAttribute
{
    public BothCommandAttribute(string alias, string buttonDescription, string? helpDescription = null)
        : base(alias, false, true, buttonDescription, helpDescription ?? buttonDescription, true, true)
    {
    }
}