using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Client;
using OneShelf.Authorization.Api.Model;

namespace OneShelf.Common.Api.WithAuthorization;

public class SecurityContext
{
    private readonly AuthorizationApiClientOptions _options;
    private CheckIdentityResponse? _checkIdentityResponse;
    private Identity? _identity;

    public const string ServiceTag = "Service";

    public SecurityContext(IOptions<AuthorizationApiClientOptions> options)
    {
        _options = options.Value;
    }

    internal void InitSuccessful(Identity identity, CheckIdentityResponse checkIdentityResponse)
    {
        if (IsInitialized)
            throw new("Already initialized.");

        _checkIdentityResponse = checkIdentityResponse;
        _identity = identity;
    }

    public void InitAnonymous()
    {
        if (IsInitialized)
            throw new("Already initialized.");

        IsAnonymous = true;
    }

    public void InitService()
    {
        if (IsInitialized)
            throw new("Already initialized.");

        IsService = true;
    }

    public bool IsAuthenticated => _identity != null;

    public bool IsAnonymous { get; private set; }
    
    public bool IsService { get; private set; }
    
    public bool IsInitialized => IsAuthenticated || IsAnonymous || IsService;

    public Identity Identity => _identity ?? throw new("Not initialized.");

    public Identity OutputIdentity
    {
        get
        {
            if (_identity != null) return _identity;

            if (IsService)
                return new()
                {
                    Hash = _options.ServiceCode,
                };

            throw new("Not initialized.");
        }
    }

    public IReadOnlyList<string> TenantTags
    {
        get
        {
            if (IsAnonymous) return new List<string>();

            if (IsService)
                return new List<string>
                {
                    ServiceTag,
                };

            return _checkIdentityResponse?.TenantTags ?? throw new("Not initialized.");
        }
    }

    public int TenantId => _checkIdentityResponse?.TenantId ?? throw new("Not initialized.");

    public bool ArePdfsAllowed => _checkIdentityResponse?.ArePdfsAllowed ?? throw new("Not initialized.");
}