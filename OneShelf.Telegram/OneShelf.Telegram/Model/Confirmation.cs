using System.ComponentModel.DataAnnotations;

namespace OneShelf.Telegram.Model;

public enum Confirmation
{
    [Display(Name = "Да")]
    Yes,

    [Display(Name = "Не совсем")]
    No,
}