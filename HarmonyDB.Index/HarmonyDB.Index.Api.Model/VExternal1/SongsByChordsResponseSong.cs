using HarmonyDB.Source.Api.Model.V1;

namespace HarmonyDB.Index.Api.Model.VExternal1;

public record SongsByChordsResponseSong
{
    public required IndexHeader Header { get; init; }

    public required float Coverage { get; init; }

    public required int? PredictedTonalityIndex { get; init; }
}