using System.Net;

namespace OneShelf.Common.Api;

public class ServiceCacheItemNotFoundException : Exception
{
    public static readonly HttpStatusCode StatusCode = (HttpStatusCode)460;
}