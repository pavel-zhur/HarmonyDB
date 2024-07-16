using System.Net;

namespace OneShelf.Common.Api.Common;

public class UnauthorizedException : Exception
{
    public static readonly HttpStatusCode StatusCode = HttpStatusCode.Unauthorized;

    public UnauthorizedException(string errorMessage) : base(errorMessage)
    {
    }
}