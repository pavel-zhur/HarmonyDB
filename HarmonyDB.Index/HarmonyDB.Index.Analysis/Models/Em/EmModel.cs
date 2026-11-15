using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Models.Em;

public record EmModel : IEmModel
{
    private readonly Dictionary<string, Song> _songs;
    private readonly Dictionary<string, Loop> _loops;

    public EmModel(IEnumerable<Song> songs,
        IEnumerable<Loop> loops)
    {
        _songs = songs.ToDictionary(x => x.Id);
        _loops = loops.ToDictionary(x => x.Id);
    }

    public IReadOnlyDictionary<string, Song> Songs => _songs;

    public IReadOnlyDictionary<string, Loop> Loops => _loops;

    IReadOnlyCollection<Song> IEmModel.Songs => _songs.Values;
    
    IReadOnlyCollection<Loop> IEmModel.Loops => _loops.Values;

    public byte[] Serialize()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        var songIds = new Dictionary<string, int>();
        var loopIds = new Dictionary<string, int>();

        writer.Write(Loops.Count);
        foreach (var (key, loop) in Loops)
        {
            loopIds[key] = loopIds.Count;
            writer.Write(key);
            SerializeSource(writer, loop);
        }

        writer.Write(Songs.Count);
        foreach (var (key, song) in Songs)
        {
            songIds[key] = songIds.Count;
            writer.Write(key);
            writer.Write(song.KnownTonality.HasValue
                ? (byte)(song.KnownTonality.Value.Tonic * 2 + (int)song.KnownTonality.Value.Scale)
                : (byte)255);

            SerializeSource(writer, song);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static void SerializeSource(BinaryWriter writer, ISource source)
    {
        writer.Write(source.Score.TonicScore);
        writer.Write(source.Score.ScaleScore);
        for (var i = 0; i < Constants.TonicCount; i++)
        {
            for (var j = 0; j < Constants.ScaleCount; j++)
            {
                writer.Write(source.TonalityProbabilities[i, j]);
            }
        }
    }
}