using HarmonyDB.Common;
using OneShelf.Common;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class GetPdfsRequestVersion
{
    public required int VersionId { get; init; }

    public required int Transpose { get; init; }

    public required NoteAlteration? Alteration { get; init; }

    public required bool TwoColumns { get; init; }
}