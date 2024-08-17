using System.ComponentModel.DataAnnotations;

namespace OneShelf.OneDog.Processor.Model;

public enum Confirmation
{
    [Display(Name = "Да")]
    Yes,

    [Display(Name = "Не совсем")]
    No,
}