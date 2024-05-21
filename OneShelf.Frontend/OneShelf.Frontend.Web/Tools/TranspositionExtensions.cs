namespace OneShelf.Frontend.Web.Tools;

public static class TranspositionExtensions
{
    public static string Transposition(this int transposition)
        => transposition switch
        {
            > 0 => $"+{transposition}",
            0 => "0",
            < 0 => transposition.ToString().Replace("-", "−"),
        };
}