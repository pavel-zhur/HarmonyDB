using OneShelf.Authorization.Api.Model;

namespace OneShelf.Common.Api.WithAuthorization;

public class SecurityContext
{
    private CheckIdentityResponse? _checkIdentityResponse;

    internal void InitSuccessful(CheckIdentityResponse checkIdentityResponse)
    {
        if (checkIdentityResponse != null)
            throw new("Already initialized.");

        _checkIdentityResponse = checkIdentityResponse;
    }

    public IReadOnlyList<string> TenantTags => _checkIdentityResponse?.TenantTags ?? throw new("Not initialized");

    public int TenantId => _checkIdentityResponse?.TenantId ?? throw new("Not initialized");

    public bool ArePdfsAllowed => _checkIdentityResponse?.ArePdfsAllowed ?? throw new("Not initialized");
}