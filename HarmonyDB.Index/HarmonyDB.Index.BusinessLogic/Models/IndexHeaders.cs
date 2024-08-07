﻿using HarmonyDB.Source.Api.Model.V1;
using OneShelf.Common;

namespace HarmonyDB.Index.BusinessLogic.Models;

public class IndexHeaders
{
    public required IReadOnlyDictionary<string, IndexHeader> Headers { get; set; }

    public byte[] Serialize()
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

        writer1.Write(Headers.Count);
        foreach (var header in Headers.Values)
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

    public static IndexHeaders Deserialize(byte[] fileModel)
    {
        using var stream = new MemoryStream(fileModel);
        using var reader = new BinaryReader(stream);

        List<string?> Read1()
        {
            var count1 = reader.ReadInt32();
            var result1 = new List<string?>();
            for (var i = 0; i < count1; i++)
            {
                result1.Add(reader.ReadString().SelectSingle(x => x == string.Empty ? null : x));
            }

            return result1;
        }

        var sources = Read1();
        var titles = Read1();
        var artists = Read1();
        var bestTonalities = Read1();

        var count = reader.ReadInt32();
        var result = new List<IndexHeader>();
        for (var i = 0; i < count; i++)
        {
            byte tonality;
            result.Add(new()
            {
                ExternalId = reader.ReadString(),
                Source = sources[reader.ReadInt32()]!,
                Title = titles[reader.ReadInt32()],
                Rating = reader.ReadSingle().SelectSingle(x => x == -1f ? (float?)null : x),
                Artists = Enumerable.Range(0, reader.ReadInt32()).Select(_ => artists[reader.ReadInt32()]!).ToList(),
                BestTonality = (tonality = reader.ReadByte()) switch
                {
                    2 => null,
                    _ => new()
                    {
                        IsReliable = tonality == 1,
                        Tonality = bestTonalities[reader.ReadInt32()]!,
                    }
                },
            });
        }

        return new()
        {
            Headers = result.ToDictionary(x => x.ExternalId),
        };
    }
}