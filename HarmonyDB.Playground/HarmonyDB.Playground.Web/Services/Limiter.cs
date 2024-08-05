using HarmonyDB.Index.Api.Model.VExternal1;

namespace HarmonyDB.Playground.Web.Services;

public class Limiter
{
    public const int MaxSongs = 300;
    public const int MaxProgressions = 200;

    public int? CheckLimit(PagedRequestBase request, PagedResponseBase response, int limit)
    {
        var fromIndex = (request.PageNumber - 1) * request.ItemsPerPage;
        var toIndex = request.PageNumber * request.ItemsPerPage - 1;

        if (toIndex < limit)
        {
            return null;
        }

        if (fromIndex >= response.Total - limit)
        {
            return null;
        }

        return limit;
    }
}