using OneShelf.Common;

namespace OneShelf.Frontend.Api.Model.V3.Databasish;

public class User
{
    public required long Id { get; init; }

    public required string Title { get; init; }

    public IEnumerable<int> DeepHash()
    {
        return Id.GetHashCode()
            .Once()
            .Append(Title.GetHashCode());
    }
}