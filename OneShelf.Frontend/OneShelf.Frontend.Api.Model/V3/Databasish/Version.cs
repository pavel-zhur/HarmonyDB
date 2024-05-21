using OneShelf.Common;

namespace OneShelf.Frontend.Api.Model.V3.Databasish;

public class Version
{
    public required int Id { get; init; }

    public required Uri Uri { get; init; }

    public required long UserId { get; init; }

    public required string? Source { get; init; }

    public required string? ExternalId { get; init; }

    public required DateTime CreatedOn { get; init; }

    public required Dictionary<long, string> Comments { get; init; }

    public Guid? CollectiveId { get; init; }

    public int? CollectiveSearchTag { get; init; }

    public VersionCollectiveType? CollectiveType { get; init; }

    public IEnumerable<int> DeepHash() =>
        Uri.GetHashCode()
            .Once()
            .Append(Id)
            .Append(UserId.GetHashCode())
            .Append(Source?.GetHashCode() ?? 0)
            .Append(ExternalId?.GetHashCode() ?? 0)
            .Append(CreatedOn.GetHashCode())
            .Concat(Comments.Select(p => p.Key.GetHashCode() ^ p.Value.GetHashCode()))
            .Append(CollectiveId?.GetHashCode() ?? 0)
            .Append(CollectiveSearchTag ?? 0);
}