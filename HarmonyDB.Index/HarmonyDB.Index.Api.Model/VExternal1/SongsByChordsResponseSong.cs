using HarmonyDB.Source.Api.Model.V1;

namespace HarmonyDB.Index.Api.Model.VExternal1;

public class SongsByChordsResponseSong
{
    public required IndexHeader Header { get; set; }

    public required float Coverage { get; set; }
}