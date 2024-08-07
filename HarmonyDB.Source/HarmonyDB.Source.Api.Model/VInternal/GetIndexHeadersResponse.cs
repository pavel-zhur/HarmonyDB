using HarmonyDB.Source.Api.Model.V1;

namespace HarmonyDB.Source.Api.Model.VInternal;

public class GetIndexHeadersResponse
{
    public required IReadOnlyList<IndexHeader> Headers { get; init; }

    public string? NextToken { get; set; }
}