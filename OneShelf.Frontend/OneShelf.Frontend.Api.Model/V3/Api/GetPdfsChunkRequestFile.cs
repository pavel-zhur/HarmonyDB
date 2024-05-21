using HarmonyDB.Common;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class GetPdfsChunkRequestFile
{
    public required string ExternalId { get; init; }

    public required string Artists { get; init; }

    public required string Title { get; init; }

    public required bool TwoColumns { get; init; }

    public required string Source { get; init; }

    public required int Transpose { get; init; }

    public required NoteAlteration? Alteration { get; init; }
}