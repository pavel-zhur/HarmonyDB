public class TestDataGeneratorParameters
{
    public int TotalSongs { get; set; } = 1000;
    public int TotalLoops { get; set; } = 1500;
    public int MinLoopsPerSong { get; set; } = 2;
    public int MaxLoopsPerSong { get; set; } = 7;
    public double KnownTonalityProbability { get; set; } = 0.15;
    public double ModulationProbability { get; set; } = 0.10;
    public double BadCycleProbability { get; set; } = 0.10;
    public double IncorrectKnownTonalityProbability { get; set; } = 0.05;
    public double SongKnownTonalityScaleMutationProbability { get; set; } = 0; // Mutation probability for KnownTonality.Scale of songs
    public double LoopLinkScaleMutationProbability { get; set; } = 0; // Mutation probability for scales in LoopLinks
    public int MinLoopAppearances { get; set; } = 1;
    public int MaxLoopAppearances { get; set; } = 4;
}