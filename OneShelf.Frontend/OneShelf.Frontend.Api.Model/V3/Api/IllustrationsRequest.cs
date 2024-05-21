using OneShelf.Authorization.Api.Model;

namespace OneShelf.Frontend.Api.Model.V3.Api;

public class IllustrationsRequest : IRequestWithIdentity
{
    public required Identity Identity { get; set; }

    public required int? Etag { get; set; }

    public required bool AllVersions { get; set; }
}