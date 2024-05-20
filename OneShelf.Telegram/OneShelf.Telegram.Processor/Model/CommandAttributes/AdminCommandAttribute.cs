namespace OneShelf.Telegram.Processor.Model.CommandAttributes;

public class AdminCommandAttribute : CommandAttribute
{
    public AdminCommandAttribute(string alias, string buttonDescription, string? helpDescription = null)
        : base(alias, false, true, buttonDescription, helpDescription ?? buttonDescription, true, false)
    {
    }
}