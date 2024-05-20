using OneShelf.Authorization.Api.Model;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class GetPdfsRequest : IRequestWithIdentity
{
    public required Identity Identity { get; set; }

    public required List<GetPdfsRequestVersion> Versions { get; set; }

    public int ChunkSize { get; set; } = 20;

    public required bool IncludeData { get; set; }

    public string? Caption { get; set; }

    public bool IncludeInspiration { get; set; }

    public bool Reindex { get; set; }
}