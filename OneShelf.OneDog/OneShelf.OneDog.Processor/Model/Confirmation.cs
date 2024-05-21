namespace OneShelf.OneDog.Processor.Model;

public enum Confirmation
{
    [StrictChoiceCaption("Да")]
    Yes,

    [StrictChoiceCaption("Не совсем")]
    No,
}