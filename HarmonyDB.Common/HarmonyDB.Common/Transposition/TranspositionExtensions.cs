namespace HarmonyDB.Common.Transposition;

public static class TranspositionExtensions
{
    public static string Transposition(this byte transposition)
        => ((int)transposition).Transposition();

    public static string Transposition(this int transposition)
        => transposition switch
        {
            > 0 => $"+{transposition}",
            0 => "0",
            < 0 => transposition.ToString().Replace("-", "−"),
        };
}