using HarmonyDB.Common.Representations.OneShelf;

namespace HarmonyDB.Index.Analysis.Tools;

public static class TonalitiesConverter
{
    public const int ProbabilitiesLength = Note.Modulus * 2;

    public static float[] ToLinear(this float[,] probabilities)
    {
        var linearArray = new float[ProbabilitiesLength];

        for (byte root = 0; root < Note.Modulus; root++)
        {
            for (var i = 0; i < 2; i++)
            {
                var isMinor = i == 1;
                var index = ToIndex(root, isMinor);
                linearArray[index] = probabilities[root, isMinor ? 1 : 0];
            }
        }

        return linearArray;
    }

    public static int ToIndex(byte root, bool isMinor) => root * 2 + (isMinor ? 1 : 0);

    public static int ToIndex(this (byte root, bool isMinor) index) => ToIndex(index.root, index.isMinor);

    public static (byte root, bool isMinor) FromIndex(this int index) => ((byte)(index / 2), index % 2 == 1);

    public static (byte Tonic, bool isMinor) GetPredictedTonality(this float[] probabilities)
    {
        return probabilities
            .Select((probability, index) => (probability, index))
            .OrderByDescending(x => x.probability)
            .Select(x => FromIndex(x.index))
            .First();
    }

    public static (byte Tonic, bool isMinor) GetSecondPredictedTonality(this float[] probabilities)
    {
        return probabilities
            .Select((probability, index) => (probability, index))
            .OrderByDescending(x => x.probability)
            .Skip(1)
            .Select(x => FromIndex(x.index))
            .First();
    }

    public static byte GetMajorTonic(this (byte tonic, bool isMinor) scale)
    {
        return scale.isMinor ? scale.GetParallelScale().tonic : scale.tonic;
    }

    public static (byte tonic, bool isMinor) GetParallelScale(this (byte tonic, bool isMinor) scale)
    {
        return scale.isMinor
            ? (Note.Normalize(scale.tonic + 3), false)
            : (Note.Normalize(scale.tonic - 3), true);
    }
    public static byte GetMajorTonic(byte tonic, bool isMinor)
        => (tonic, isMinor).GetMajorTonic();

    public static (byte tonic, bool isMinor) GetParallelScale(byte tonic, bool isMinor)
        => (tonic, isMinor).GetParallelScale();

    public static float TonalityConfidence(this float[] probabilities)
        => probabilities.Max();

    public static float TonicConfidence(this float[] probabilities)
        => probabilities
            .Take(Note.Modulus)
            .Select((x, i) => x + probabilities[i.FromIndex().GetParallelScale().ToIndex()])
            .Max();
}