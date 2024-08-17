namespace OneShelf.Telegram.Model;

public class CommandAttribute : Attribute
{
    public CommandAttribute(string alias, string buttonDescription, Role role, string? helpDescription = null)
        : this(alias, false, true, buttonDescription, helpDescription ?? buttonDescription, role)
    {
    }

    public CommandAttribute(string alias, bool supportsParameters, bool supportsNoParameters, string buttonDescription, string helpDescription, Role role)
    {
        Alias = alias;
        SupportsParameters = supportsParameters;
        SupportsNoParameters = supportsNoParameters;
        ButtonDescription = buttonDescription;
        HelpDescription = helpDescription;
        Role = role;
    }

    public string Alias { get; }
    public bool SupportsParameters { get; init; }
    public bool SupportsNoParameters { get; init; }
    public string ButtonDescription { get; }
    public string HelpDescription { get; }
    public Role Role { get; }
}