using System.Text;
using HarmonyDB.Index.Analysis.Models.Ids;
using HarmonyDB.Source.Api.Model.V1;

namespace HarmonyDB.Index.BusinessLogic.Models;

/// <summary>
/// The songs' headers in arrays, in the order they were serialized: each distinct source, title, artist and tonality
/// kept once, each header a small record of their indices. An <see cref="IndexHeader"/> is made when asked for.
/// </summary>
public class IndexHeaders
{
    private const byte UnknownTonality = 2;

    private readonly IdTable _songIds;
    private readonly string?[] _sources;
    private readonly string?[] _titles;
    private readonly string?[] _artists;
    private readonly string?[] _bestTonalities;
    private readonly Header[] _headers;
    private readonly int[] _headersArtists;

    private IndexHeaders(IdTable songIds, IdPositions songs, string?[] sources, string?[] titles, string?[] artists, string?[] bestTonalities, Header[] headers, int[] headersArtists)
    {
        _songIds = songIds;
        Songs = songs;
        _sources = sources;
        _titles = titles;
        _artists = artists;
        _bestTonalities = bestTonalities;
        _headers = headers;
        _headersArtists = headersArtists;
    }

    /// <summary>
    /// The songs (numbers of the songs' ids) in the headers' order.
    /// </summary>
    public IdPositions Songs { get; }

    public int Count => Songs.Count;

    public float? GetRating(int position) => ToRating(_headers[position].Rating);

    public IndexHeader Get(int position)
    {
        ref readonly var header = ref _headers[position];
        var artists = new string[header.ArtistsCount];
        for (var i = 0; i < artists.Length; i++)
        {
            artists[i] = _artists[_headersArtists[header.ArtistsStart + i]]!;
        }

        return new()
        {
            ExternalId = _songIds[Songs.GetNumber(position)],
            Source = _sources[header.Source]!,
            Title = _titles[header.Title],
            Rating = ToRating(header.Rating),
            Artists = artists,
            BestTonality = header.BestTonalityReliability switch
            {
                UnknownTonality => null,
                _ => new()
                {
                    IsReliable = header.BestTonalityReliability == 1,
                    Tonality = _bestTonalities[header.BestTonality]!,
                },
            },
        };
    }

    public static byte[] Serialize(IReadOnlyCollection<IndexHeader> headers)
    {
        var sources = new Dictionary<string, int>();
        var titles = new Dictionary<string, int>();
        var artists = new Dictionary<string, int>();
        var bestTonalities = new Dictionary<string, int>();

        int Add(string value, Dictionary<string, int> dictionary) => dictionary.TryGetValue(value, out var result)
            ? result
            : dictionary[value] = dictionary.Count;

        using var stream1 = new MemoryStream();
        using var stream2 = new MemoryStream();
        using var writer1 = new BinaryWriter(stream1);
        using var writer2 = new BinaryWriter(stream2);

        writer1.Write(headers.Count);
        foreach (var header in headers)
        {
            writer1.Write(header.ExternalId);
            writer1.Write(Add(header.Source, sources));
            writer1.Write(Add(header.Title ?? string.Empty, titles));
            writer1.Write(header.Rating ?? -1f);
            var artistsOutput = header.Artists?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [];
            writer1.Write(artistsOutput.Count);
            foreach (var artist in artistsOutput)
            {
                writer1.Write(Add(artist, artists));
            }

            writer1.Write((byte)(header.BestTonality?.IsReliable switch
            {
                true => 1,
                false => 0,
                null => 2,
            }));

            if (header.BestTonality != null)
            {
                writer1.Write(Add(header.BestTonality.Tonality, bestTonalities));
            }
        }

        void Write(Dictionary<string, int> catalog)
        {
            writer2.Write(catalog.Count);
            foreach (var item in catalog.OrderBy(x => x.Value).Select(x => x.Key))
            {
                writer2.Write(item);
            }
        }

        Write(sources);
        Write(titles);
        Write(artists);
        Write(bestTonalities);

        return stream2.ToArray().Concat(stream1.ToArray()).ToArray();
    }

    /// <summary>
    /// Reads what <see cref="Serialize"/> wrote, straight into the final arrays.
    /// </summary>
    /// <exception cref="ArgumentException">An external id repeats.</exception>
    public static IndexHeaders Read(Stream stream, IdTable songIds)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        string?[] ReadCatalog()
        {
            var result = new string?[reader.ReadInt32()];
            for (var i = 0; i < result.Length; i++)
            {
                var value = reader.ReadString();
                result[i] = value == string.Empty ? null : value;
            }

            return result;
        }

        var sources = ReadCatalog();
        var titles = ReadCatalog();
        var artists = ReadCatalog();
        var bestTonalities = ReadCatalog();

        var count = reader.ReadInt32();
        var numbers = new int[count];
        var headers = new Header[count];
        var headersArtists = new List<int>();
        for (var i = 0; i < count; i++)
        {
            numbers[i] = songIds.GetOrAdd(reader.ReadString());
            var source = reader.ReadInt32();
            var title = reader.ReadInt32();
            var rating = reader.ReadSingle();

            var artistsStart = headersArtists.Count;
            var artistsCount = reader.ReadInt32();
            for (var j = 0; j < artistsCount; j++)
            {
                headersArtists.Add(Check(reader.ReadInt32(), artists));
            }

            var bestTonalityReliability = reader.ReadByte();
            var bestTonality = bestTonalityReliability == UnknownTonality ? -1 : Check(reader.ReadInt32(), bestTonalities);

            headers[i] = new(Check(source, sources), Check(title, titles), rating, artistsStart, artistsCount, bestTonality, bestTonalityReliability);
        }

        return new(songIds, new(numbers), sources, titles, artists, bestTonalities, headers, headersArtists.ToArray());
    }

    private static float? ToRating(float rating) => rating == -1f ? null : rating;

    private static int Check(int index, string?[] catalog)
        => index >= 0 && index < catalog.Length ? index : throw new ArgumentOutOfRangeException(nameof(index), index, "The index is out of the catalog.");

    private readonly record struct Header(
        int Source,
        int Title,
        float Rating,
        int ArtistsStart,
        int ArtistsCount,
        int BestTonality,
        byte BestTonalityReliability);
}
