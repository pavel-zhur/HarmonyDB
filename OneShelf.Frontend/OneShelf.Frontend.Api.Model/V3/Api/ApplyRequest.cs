using OneShelf.Authorization.Api.Model;
using OneShelf.Frontend.Api.Model.V3.Instant;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class ApplyRequest : IRequestWithIdentity
{
    public required Identity Identity { get; set; }

    public List<VisitedChords> VisitedChords { get; init; } = new();

    public List<VisitedSearch> VisitedSearches { get; init; } = new();

    public List<ImportedVersion> ImportedVersions { get; init; } = new();

    public List<RemovedVersion> RemovedVersions { get; init; } = new();

    public List<UpdatedLike> UpdatedLikes { get; init; } = new();

    public int? Etag { get; set; }

    public void AddFrom(ApplyRequest another)
    {
        ImportedVersions.AddRange(another.ImportedVersions);
        RemovedVersions.AddRange(another.RemovedVersions);
        UpdatedLikes.AddRange(another.UpdatedLikes);
        VisitedChords.AddRange(another.VisitedChords);
        VisitedSearches.AddRange(another.VisitedSearches);
    }

    public IEnumerable<Guid> GetIds() =>
        UpdatedLikes
            .Cast<ActionBase>()
            .Concat(ImportedVersions)
            .Concat(RemovedVersions)
            .Concat(VisitedSearches)
            .Concat(VisitedChords)
            .Select(x => x.ActionId);
}