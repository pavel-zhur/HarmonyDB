using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HarmonyDB.Shelf.Database.Model;

/// <summary>
/// A client registered with the shelf as an OAuth 2.1 authorization server (dynamic client registration).
/// </summary>
public class OAuthClient
{
    public required string ClientId { get; set; }

    public required string ClientName { get; set; }

    public List<string> RedirectUris { get; set; } = [];

    public List<string> GrantTypes { get; set; } = [];

    /// <summary>
    /// "none" for a public client; otherwise how a confidential one proves itself.
    /// </summary>
    public required string TokenEndpointAuthMethod { get; set; }

    /// <summary>
    /// The hash of a confidential client's secret.
    /// </summary>
    public byte[]? ClientSecretHash { get; set; }

    public string? ClientUri { get; set; }

    public string? LogoUri { get; set; }

    public string? SoftwareId { get; set; }

    public string? SoftwareVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}

/// <summary>
/// What a person allowed a client (the consent): a set of permissions. What the client may really do is this set and
/// what the admin gave the person, intersected on every call. Removing the grant disconnects the client.
/// </summary>
public class OAuthGrant
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public required string ClientId { get; set; }

    public OAuthClient Client { get; set; } = null!;

    public List<string> Permissions { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}

/// <summary>
/// An authorization code given out under a grant (authorization code + PKCE), kept hashed, used once.
/// </summary>
public class OAuthAuthorizationCode
{
    public required byte[] CodeHash { get; set; }

    public Guid GrantId { get; set; }

    public OAuthGrant Grant { get; set; } = null!;

    /// <summary>
    /// The family of the tokens this code is exchanged for (<see cref="OAuthToken.FamilyId"/>): a code used a second
    /// time revokes them.
    /// </summary>
    public Guid FamilyId { get; set; }

    public required string RedirectUri { get; set; }

    public required string CodeChallenge { get; set; }

    public required string CodeChallengeMethod { get; set; }

    /// <summary>
    /// The resource the token will be for (RFC 8707), when the client named one.
    /// </summary>
    public string? Resource { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}

public enum OAuthTokenKind
{
    Access,
    Refresh,
}

/// <summary>
/// An access or refresh token given out under a grant, kept hashed. A refresh token is used once and replaced
/// (rotation); the tokens descended from one authorization share a <see cref="FamilyId"/>, so a reused refresh token
/// revokes its family.
/// </summary>
public class OAuthToken
{
    public required byte[] TokenHash { get; set; }

    public Guid GrantId { get; set; }

    public OAuthGrant Grant { get; set; } = null!;

    public OAuthTokenKind Kind { get; set; }

    public Guid FamilyId { get; set; }

    public string? Resource { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// When a refresh token was used (and replaced).
    /// </summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}

/// <summary>
/// A key for programs that cannot sign in: made by the person with a choice of what it may do, shown once, kept hashed,
/// with the expiry the person picked (null: none). Its permissions are intersected with the admin's on every call, as a
/// grant's are.
/// </summary>
public class PersonalKey
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public required string Name { get; set; }

    public required byte[] KeyHash { get; set; }

    /// <summary>
    /// The first characters of the key, shown so the person can tell their keys apart.
    /// </summary>
    public required string Prefix { get; set; }

    public List<string> Permissions { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}

internal sealed class OAuthClientConfiguration : IEntityTypeConfiguration<OAuthClient>
{
    public void Configure(EntityTypeBuilder<OAuthClient> builder)
    {
        builder.ToTable("oauth_clients");
        builder.HasKey(x => x.ClientId);
    }
}

internal sealed class OAuthGrantConfiguration : IEntityTypeConfiguration<OAuthGrant>
{
    public void Configure(EntityTypeBuilder<OAuthGrant> builder)
    {
        builder.ToTable("oauth_grants");
        builder.HasIndex(x => new { x.AccountId, x.ClientId }).IsUnique();
        builder.HasIndex(x => x.ClientId);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OAuthAuthorizationCodeConfiguration : IEntityTypeConfiguration<OAuthAuthorizationCode>
{
    public void Configure(EntityTypeBuilder<OAuthAuthorizationCode> builder)
    {
        builder.ToTable("oauth_authorization_codes");
        builder.HasKey(x => x.CodeHash);
        builder.HasIndex(x => x.GrantId);
        builder.HasIndex(x => x.FamilyId).IsUnique();
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasOne(x => x.Grant).WithMany().HasForeignKey(x => x.GrantId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OAuthTokenConfiguration : IEntityTypeConfiguration<OAuthToken>
{
    public void Configure(EntityTypeBuilder<OAuthToken> builder)
    {
        builder.ToTable("oauth_tokens");
        builder.HasKey(x => x.TokenHash);
        builder.HasIndex(x => x.GrantId);
        builder.HasIndex(x => x.FamilyId);
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasOne(x => x.Grant).WithMany().HasForeignKey(x => x.GrantId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PersonalKeyConfiguration : IEntityTypeConfiguration<PersonalKey>
{
    public void Configure(EntityTypeBuilder<PersonalKey> builder)
    {
        builder.ToTable("personal_keys");
        builder.HasIndex(x => x.KeyHash).IsUnique();
        builder.HasIndex(x => x.AccountId);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
