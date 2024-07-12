using System.Net;

namespace OneShelf.Common.Api.Client;

public class CacheItemNotFoundException : Exception
{
    public static readonly HttpStatusCode StatusCode = (HttpStatusCode)460;
}