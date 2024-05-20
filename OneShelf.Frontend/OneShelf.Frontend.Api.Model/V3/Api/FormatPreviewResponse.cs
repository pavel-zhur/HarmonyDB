using HarmonyDB.Common.Representations.OneShelf;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class FormatPreviewResponse
{
    public required NodeHtml Output { get; init; }
}