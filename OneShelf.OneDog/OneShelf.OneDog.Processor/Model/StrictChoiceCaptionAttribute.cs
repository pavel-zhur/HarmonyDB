namespace OneShelf.OneDog.Processor.Model;

[AttributeUsage(AttributeTargets.Field)]
public class StrictChoiceCaptionAttribute : Attribute
{
    public StrictChoiceCaptionAttribute(string caption) => Caption = caption;
    public string Caption { get; }
}