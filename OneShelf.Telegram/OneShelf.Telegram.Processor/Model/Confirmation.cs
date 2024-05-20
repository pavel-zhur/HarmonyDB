using OneShelf.Pdfs.Generation.Inspiration.Models;

namespace OneShelf.Telegram.Processor.Model;

public enum Confirmation
{
    [StrictChoiceCaption("Да")]
    Yes,

    [StrictChoiceCaption("Не совсем")]
    No,
}