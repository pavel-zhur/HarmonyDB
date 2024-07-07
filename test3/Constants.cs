public static class Constants
{
    public const int TonalityCount = 24; // 12 major + 12 minor

    // Method to get parallel tonality
    public static int GetParallelTonality(int tonality)
    {
        if (tonality < 12) // major
        {
            return (tonality + 9) % 12 + 12; // corresponding minor
        }
        else // minor
        {
            return (tonality - 12 + 3) % 12; // corresponding major
        }
    }
}