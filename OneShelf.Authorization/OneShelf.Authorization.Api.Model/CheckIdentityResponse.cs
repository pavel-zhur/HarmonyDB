namespace OneShelf.Authorization.Api.Model;

public class CheckIdentityResponse
{
    public required int? TenantId { get; init; }

    public required IReadOnlyList<string>? TenantTags { get; init; }

    public required bool IsSuccess { get; init; }

    public required string? AuthorizationError { get; init; }

    public required bool? ArePdfsAllowed { get; init; }
}