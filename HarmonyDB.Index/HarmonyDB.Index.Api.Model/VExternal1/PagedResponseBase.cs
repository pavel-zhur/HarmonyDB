namespace HarmonyDB.Index.Api.Model.VExternal1;

public record PagedResponseBase
{
    public required int Total { get; init; }
    public required int TotalPages { get; init; }
    public required int CurrentPageNumber { get; init; }
}