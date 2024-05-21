namespace OneShelf.Collectives.Api.Model.VInternal;

public class GetRequest
{
    public Guid? CollectiveId { get; set; }

    public Uri? CollectiveUri { get; set; }
}