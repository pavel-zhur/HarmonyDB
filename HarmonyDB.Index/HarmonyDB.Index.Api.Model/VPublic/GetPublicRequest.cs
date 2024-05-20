using HarmonyDB.Common;
using OneShelf.Common;

namespace HarmonyDB.Index.Api.Model.VPublic;

public class GetPublicRequest
{
    public required string Url { get; init; }

    public int Transpose { get; init; }

    public NoteAlteration? Alteration { get; init; }
}