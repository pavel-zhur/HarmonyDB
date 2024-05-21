namespace OneShelf.Authorization.Api.Model;

public interface IRequestWithIdentity
{
    Identity Identity { get; }
}