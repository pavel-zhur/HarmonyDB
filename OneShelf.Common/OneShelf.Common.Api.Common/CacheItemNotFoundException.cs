using System.Net;

namespace OneShelf.Common.Api.Common;

public class CacheItemNotFoundException : Exception
{
    public static readonly HttpStatusCode StatusCode = (HttpStatusCode)460;
}