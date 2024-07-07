public static class Constants
{
    public const int TonalityCount = 24; // 12 major + 12 minor
    public const int TonalityCountSmall = 12; // 12 major + 12 minor

    public static int GetCombinedTonality(int tonality)
    {
        return tonality / 2;
    }

    public static (int shift, bool isMajor) FromTonality(this int tonality) => (tonality / 2, tonality % 2 == 0);
    public static int ToTonality(int shift, bool isMajor) => shift * 2 + (isMajor ? 1 : 0);

    // Method to get parallel tonality
    public static int GetParallelTonality(int tonality)
    {
        return GetCombinedTonality(tonality) * 2
            + (tonality % 2 == 0 ? 1 : 0);
    }
}