using HarmonyDB.Source.Api.Model.V1;

namespace HarmonyDB.Index.Api.Model.VExternal1.Main;

public record SongsByHeaderResponseSong(IndexHeader Header, int? PredictedTonalityIndex);