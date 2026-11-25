using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Common.ServiceAuth;

/// <summary>
/// Authenticates a request that carries <see cref="InternalKeyDefaults.HeaderName"/> as the internal client whose key it
/// is. A request without the header gets no result from this scheme (it is not an internal call); a request with an
/// unknown key fails.
/// </summary>
public sealed class InternalKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<InternalClientsOptions> clients)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(InternalKeyDefaults.HeaderName, out var values))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (values.Count != 1 || string.IsNullOrEmpty(values[0]))
            return Task.FromResult(AuthenticateResult.Fail("The internal key header is malformed."));

        var client = Find(values[0]!);
        if (client == null)
            return Task.FromResult(AuthenticateResult.Fail("The internal key is not known."));

        var identity = new ClaimsIdentity(
            client.Scopes
                .Select(scope => new Claim(InternalKeyDefaults.ScopeClaimType, scope))
                .Prepend(new(InternalKeyDefaults.ClientClaimType, client.Name)),
            Scheme.Name,
            InternalKeyDefaults.ClientClaimType,
            null);

        return Task.FromResult(AuthenticateResult.Success(new(new(identity), Scheme.Name)));
    }

    /// <summary>
    /// Compares the presented key with every configured one in constant time: the hashes have one length, and the loop
    /// does not stop at a match, so the time taken tells nothing about the keys.
    /// </summary>
    private InternalClient? Find(string presentedKey)
    {
        var presented = SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey));
        InternalClient? found = null;
        foreach (var client in clients.CurrentValue.Clients)
        {
            var known = SHA256.HashData(Encoding.UTF8.GetBytes(client.Key));
            if (CryptographicOperations.FixedTimeEquals(presented, known))
            {
                found = client;
            }
        }

        return found;
    }
}
