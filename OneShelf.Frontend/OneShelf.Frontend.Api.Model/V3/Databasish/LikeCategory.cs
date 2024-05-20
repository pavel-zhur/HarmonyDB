using OneShelf.Common;

namespace OneShelf.Frontend.Api.Model.V3.Databasish;

public class LikeCategory
{
    public required int Id { get; init; }

    public required long UserId { get; init; }

    public required string Name { get; init; }

    public required string CssColor { get; init; }

    public required int Order { get; init; }

    public required float PrivateWeight { get; init; }

    public required string CssIcon { get; init; }

    public required LikeCategoryAccess Access { get; init; }

    public IEnumerable<int> DeepHash()
    {
        return Id
            .Once()
            .Append(UserId.GetHashCode())
            .Append(CssColor.GetHashCode())
            .Append(Order)
            .Append(PrivateWeight.GetHashCode())
            .Append(CssIcon.GetHashCode())
            .Append(Access.GetHashCode())
            .Append(Name.GetHashCode());
    }
}