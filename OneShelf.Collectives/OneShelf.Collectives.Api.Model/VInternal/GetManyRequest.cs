namespace OneShelf.Collectives.Api.Model.VInternal;

public class GetManyRequest
{
    public required List<Guid> CollectiveIds { get; set; }
}
