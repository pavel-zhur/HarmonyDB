namespace OneShelf.Authorization.Api.Model;

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string errorMessage) : base(errorMessage)
    {
    }
}