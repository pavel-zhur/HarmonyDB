using System.Net;

namespace OneShelf.Common.Api.Common;

public class ConcurrencyException : Exception
{
    public static readonly HttpStatusCode StatusCode = HttpStatusCode.TooManyRequests;
}