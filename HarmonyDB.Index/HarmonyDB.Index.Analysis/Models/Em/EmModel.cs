﻿using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Models.Em;

public record EmModel : IEmModel
{
    private readonly Dictionary<string, Song> _songs;
    private readonly Dictionary<string, Loop> _loops;

    public EmModel(IEnumerable<Song> songs,
        IEnumerable<Loop> loops,
        IReadOnlyList<LoopLink> loopLinks)
    {
        _songs = songs.ToDictionary(x => x.Id);
        _loops = loops.ToDictionary(x => x.Id);
        LoopLinks = loopLinks;
        LoopLinksBySongId = loopLinks.ToLookup(x => x.SongId);
        LoopLinksByLoopId = loopLinks.ToLookup(x => x.LoopId);
    }

    public IReadOnlyDictionary<string, Song> Songs => _songs;

    public IReadOnlyDictionary<string, Loop> Loops => _loops;

    public IReadOnlyList<LoopLink> LoopLinks { get; }

    IReadOnlyCollection<ISong> IEmModel.Songs => _songs.Values;
    
    IReadOnlyCollection<ILoop> IEmModel.Loops => _loops.Values;

    IReadOnlyList<ILoopLink> IEmModel.LoopLinks => LoopLinks;

    public ILookup<string, LoopLink> LoopLinksBySongId { get; }

    public ILookup<string, LoopLink> LoopLinksByLoopId { get; }

    public static EmModel Deserialize(byte[] serialized)
    {
        using var stream = new MemoryStream(serialized);
        using var reader = new BinaryReader(stream);

        var loopsKeysPartial = Enumerable
            .Range(0, reader.ReadInt32())
            .Select(i =>
            {
                var id = reader.ReadString();
                var sequence = Analysis.Models.Loop.Deserialize(id);
                var loop = new Loop
                {
                    Id = id,
                    Length = sequence.Length,
                };

                DeserializeSource(reader, loop);

                return loop;
            })
            .ToList();

        var songsKeysPartial = Enumerable
            .Range(0, reader.ReadInt32())
            .Select(i =>
            {
                var id = reader.ReadString();
                var scale = reader.ReadByte();
                
                (int Tonic, Scale scale) knownTonality;
                bool isTonalityKnown;
                if (scale == 2)
                {
                    isTonalityKnown = false;
                    knownTonality = default;
                }
                else
                {
                    isTonalityKnown = true;
                    knownTonality = (reader.ReadInt32(), (Scale)scale);
                }

                var song = new Song
                {
                    Id = id,
                    IsTonalityKnown = isTonalityKnown,
                    KnownTonality = knownTonality,
                };

                DeserializeSource(reader, song);

                return song;
            })
            .ToList();

        var count = reader.ReadInt32();
        var all = new List<LoopLink>();
        for (var i = 0; i < count; i++)
        {
            all.Add(new()
            {
                Loop = loopsKeysPartial[reader.ReadInt32()],
                Song = songsKeysPartial[reader.ReadInt32()],
                Shift = reader.ReadInt32(),
                Occurrences = reader.ReadInt16(),
                Successions = reader.ReadInt16(),
            });
        }

        return new(songsKeysPartial, loopsKeysPartial, all);
    }

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
            if (song.IsTonalityKnown)
            {
                writer.Write((byte)song.KnownTonality.Scale);
                writer.Write(song.KnownTonality.Tonic);
            }
            else
            {
                writer.Write((byte)2);
            }

            SerializeSource(writer, song);
        }

        writer.Write(LoopLinks.Count);
        foreach (var loopLink in LoopLinks)
        {
            writer.Write(loopIds[loopLink.LoopId]);
            writer.Write(songIds[loopLink.SongId]);
            writer.Write(loopLink.Shift);
            writer.Write(loopLink.Occurrences);
            writer.Write(loopLink.Successions);
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

    private static void DeserializeSource(BinaryReader reader, ISource source)
    {
        source.Score = (reader.ReadDouble(), reader.ReadDouble());

        for (var i = 0; i < Constants.TonicCount; i++)
        {
            for (var j = 0; j < Constants.ScaleCount; j++)
            {
                source.TonalityProbabilities[i, j] = reader.ReadDouble();
            }
        }
    }
}