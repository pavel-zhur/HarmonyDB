namespace HarmonyDB.Index.Api.Model.VExternal1;

public record PagedRequestBase
{
    public int PageNumber { get; init; } = 1;
    public int ItemsPerPage { get; init; } = 100;
}