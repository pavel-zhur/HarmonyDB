namespace OneShelf.Telegram.Processor.Model.CommandAttributes;

public class CommandAttribute : Attribute
{
    public CommandAttribute(string alias, bool supportsParameters, bool supportsNoParameters, string buttonDescription, string helpDescription, bool appliesToAdmins, bool appliesToRegular)
    {
        if (!appliesToAdmins && !appliesToRegular)
        {
            throw new ArgumentException("need to apply somewhere.");
        }

        Alias = alias;
        SupportsParameters = supportsParameters;
        SupportsNoParameters = supportsNoParameters;
        ButtonDescription = buttonDescription;
        HelpDescription = helpDescription;
        AppliesToAdmins = appliesToAdmins;
        AppliesToRegular = appliesToRegular;
    }

    public string Alias { get; }
    public bool SupportsParameters { get; }
    public bool SupportsNoParameters { get; }
    public string ButtonDescription { get; }
    public string HelpDescription { get; }
    public bool AppliesToAdmins { get; }
    public bool AppliesToRegular { get; }
}