namespace OneShelf.Telegram.Model.CommandAttributes;

public class BothCommandAttribute : CommandAttribute
{
    public BothCommandAttribute(string alias, string buttonDescription, string? helpDescription = null)
        : base(alias, false, true, buttonDescription, helpDescription ?? buttonDescription, Role.Regular)
    {
    }
}