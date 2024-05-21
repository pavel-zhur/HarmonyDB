using OneShelf.Common;
using OneShelf.Common.Songs;

namespace OneShelf.Frontend.Api.Model.V3.Databasish;

public class Like : ILike
{
    public required long UserId { get; init; }

    public required byte Level { get; init; }

    public required DateTime CreatedOn { get; init; }

    public required int? Transpose { get; init; }

    public required int? VersionId { get; init; }

    public required int? LikeCategoryId { get; init; }

    public IEnumerable<int> DeepHash() =>
        UserId.GetHashCode().Once()
            .Append(Level)
            .Append(CreatedOn.GetHashCode())
            .Append(Transpose ?? -122)
            .Append(VersionId ?? 0)
            .Append(LikeCategoryId ?? 0);
}