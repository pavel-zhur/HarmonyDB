using HarmonyDB.Common.Representations.OneShelf;

namespace OneShelf.Sources.Self.Api.Model.V1;

public class FormatPreviewResponse
{
    public required NodeHtml Output { get; init; }
}