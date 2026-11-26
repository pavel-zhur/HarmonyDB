using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HarmonyDB.Shelf.Database.Model;

/// <summary>
/// A person, or a system account (no sign-in) that owns what the shelf itself publishes.
/// A profile is deleted in two steps (see <see cref="DeletedAt"/>); the row is hard-deleted only by the purge.
/// </summary>
public class Account
{
    public Guid Id { get; set; }

    public required string DisplayName { get; set; }

    public string? PictureUrl { get; set; }

    /// <summary>
    /// The language of the interface: "en", "ru"; null until chosen (the browser's is used).
    /// </summary>
    public string? Locale { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    /// <summary>
    /// When the person deleted their profile. Step one, in one transaction: their shared playlists pass to another
    /// member, everything of theirs that others see (memberships, marks, keys, notes, their own playlists) is marked
    /// removed (<see cref="ITombstoned"/>), their sign-ins, sessions, grants, keys and music links are deleted, and the
    /// name and picture are cleared. Step two, the purge, deletes the row (and by cascade the tombstones) once the
    /// tombstones' time is over. No other code deletes an account.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    public List<AccountLogin> Logins { get; set; } = [];

    public List<AccountPermission> Permissions { get; set; } = [];

    public AccountSettings? Settings { get; set; }
}

/// <summary>
/// A sign-in linked to an account: one identity of one provider. An account may have many; an identity belongs to one
/// account.
/// </summary>
public class AccountLogin
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    /// <summary>
    /// "telegram", "google", "yandex", "dev".
    /// </summary>
    public required string Provider { get; set; }

    /// <summary>
    /// The provider's stable id of the identity.
    /// </summary>
    public required string Subject { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>
    /// The identity's user name at the provider, when it has one (a contact the playlist's members see).
    /// </summary>
    public string? Username { get; set; }

    public string? Email { get; set; }

    public string? PictureUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSignInAt { get; set; }
}

/// <summary>
/// A browser session. The browser holds an opaque token; only its hash is kept.
/// </summary>
public class Session
{
    public Guid Id { get; set; }

    public required byte[] TokenHash { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the person last signed in within this session (a fresh sign-in is asked before merging accounts).
    /// </summary>
    public DateTimeOffset SignedInAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public string? UserAgent { get; set; }

    public string? IpAddress { get; set; }
}

public enum SignInFlowPurpose
{
    /// <summary>Signing in (or up).</summary>
    SignIn,

    /// <summary>Adding a sign-in to the account of the session that started the flow.</summary>
    Link,
}

/// <summary>
/// A sign-in at a provider under way: its state, and what it needs to finish. Bound to the session that started it
/// when it links an identity to that session's account.
/// </summary>
public class SignInFlow
{
    /// <summary>
    /// The state sent to the provider and given back by it.
    /// </summary>
    public required string State { get; set; }

    public required string Provider { get; set; }

    public SignInFlowPurpose Purpose { get; set; }

    public Guid? SessionId { get; set; }

    public Session? Session { get; set; }

    /// <summary>
    /// The PKCE verifier, for providers that take PKCE.
    /// </summary>
    public string? CodeVerifier { get; set; }

    public string? Nonce { get; set; }

    /// <summary>
    /// Where in the app to return to.
    /// </summary>
    public string? ReturnPath { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// When the provider gave the identity back.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// The identity the provider gave back, when it already belonged to another account (the merge is then asked).
    /// </summary>
    public Guid? CompletedLoginId { get; set; }

    public AccountLogin? CompletedLogin { get; set; }
}

/// <summary>
/// A permission an admin gave a person: a built-in one, or a custom key from configuration.
/// </summary>
public class AccountPermission
{
    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public required string Permission { get; set; }

    public DateTimeOffset GrantedAt { get; set; }

    /// <summary>
    /// Who gave it: an internal client's name, or a tool's.
    /// </summary>
    public string? GrantedBy { get; set; }
}

public enum MusicService
{
    Yandex,

    /// <summary>Kept as "youtube".</summary>
    Youtube,
}

public enum MusicCredentialKind
{
    /// <summary>A token.</summary>
    Token,

    /// <summary>The headers of a request of the service's own site.</summary>
    Headers,
}

/// <summary>
/// A person's music account, linked by what the person copied from the browser. Kept encrypted (AES-GCM under the key
/// of the configuration), never given back to the browser; used only for the person's own playlists, likes and history.
/// Its id is the subject under which the music service caches the person's answers.
/// </summary>
public class MusicLink
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public MusicService Service { get; set; }

    public MusicCredentialKind CredentialKind { get; set; }

    public required byte[] Ciphertext { get; set; }

    public required byte[] Nonce { get; set; }

    public required byte[] Tag { get; set; }

    /// <summary>
    /// Which key encrypted it: the secrets key of the configuration has versions, so it can be rotated (new links
    /// take the current one; old ones are read with theirs and encrypted anew).
    /// </summary>
    public int KeyVersion { get; set; }

    /// <summary>
    /// The account's name on the service, to show which one is linked.
    /// </summary>
    public string? ExternalName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}

public enum AccidentalsMode
{
    /// <summary>As each chord is written.</summary>
    AsWritten,
    Flats,
    Sharps,
}

public enum ThemeMode
{
    System,
    Light,
    Dark,
}

/// <summary>
/// A person's settings of the app, kept in the profile.
/// </summary>
public class AccountSettings
{
    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    /// <summary>
    /// The size of the chords (pinch on the chords).
    /// </summary>
    public double ChordsScale { get; set; } = 1;

    /// <summary>
    /// The size of the whole interface (pinch elsewhere).
    /// </summary>
    public double InterfaceScale { get; set; } = 1;

    /// <summary>
    /// "Less": a tap on a chord shows a small plate instead of the window of fingerings.
    /// </summary>
    public bool LessMode { get; set; }

    public AccidentalsMode Accidentals { get; set; } = AccidentalsMode.AsWritten;

    /// <summary>
    /// Show the degrees of the chords, where they are known.
    /// </summary>
    public bool ShowDegrees { get; set; } = true;

    /// <summary>
    /// Draw the alterations of chord names as superscripts and subscripts.
    /// </summary>
    public bool ChordSuperscripts { get; set; } = true;

    public ThemeMode Theme { get; set; } = ThemeMode.System;

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasOne(x => x.Settings).WithOne(x => x.Account).HasForeignKey<AccountSettings>(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AccountLoginConfiguration : IEntityTypeConfiguration<AccountLogin>
{
    public void Configure(EntityTypeBuilder<AccountLogin> builder)
    {
        builder.ToTable("account_logins");
        builder.HasIndex(x => new { x.Provider, x.Subject }).IsUnique();
        builder.HasIndex(x => x.AccountId);
        builder.HasOne(x => x.Account).WithMany(x => x.Logins).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SignInFlowConfiguration : IEntityTypeConfiguration<SignInFlow>
{
    public void Configure(EntityTypeBuilder<SignInFlow> builder)
    {
        builder.ToTable("sign_in_flows");
        builder.HasKey(x => x.State);
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CompletedLogin).WithMany().HasForeignKey(x => x.CompletedLoginId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AccountPermissionConfiguration : IEntityTypeConfiguration<AccountPermission>
{
    public void Configure(EntityTypeBuilder<AccountPermission> builder)
    {
        builder.ToTable("account_permissions");
        builder.HasKey(x => new { x.AccountId, x.Permission });
        builder.HasIndex(x => x.Permission);
        builder.HasOne(x => x.Account).WithMany(x => x.Permissions).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MusicLinkConfiguration : IEntityTypeConfiguration<MusicLink>
{
    public void Configure(EntityTypeBuilder<MusicLink> builder)
    {
        builder.ToTable("music_links");
        builder.HasIndex(x => new { x.AccountId, x.Service }).IsUnique();
        builder.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AccountSettingsConfiguration : IEntityTypeConfiguration<AccountSettings>
{
    public void Configure(EntityTypeBuilder<AccountSettings> builder)
    {
        builder.ToTable("account_settings", x =>
        {
            x.HasCheckConstraint("ck_account_settings_chords_scale", "chords_scale > 0");
            x.HasCheckConstraint("ck_account_settings_interface_scale", "interface_scale > 0");
        });
        builder.HasKey(x => x.AccountId);
    }
}
/// <summary>
/// That one account was merged into another: the merged account's own name stays one of the other's names (as the
/// sources know people by), for as long as the other lives. A merge into an account later merged again points at the
/// last one.
/// </summary>
public class AccountMerge
{
    /// <summary>
    /// The account merged away.
    /// </summary>
    public Guid MergedId { get; set; }

    /// <summary>
    /// The account it lives on in.
    /// </summary>
    public Guid IntoId { get; set; }

    public DateTimeOffset MergedAt { get; set; }
}

internal sealed class AccountMergeConfiguration : IEntityTypeConfiguration<AccountMerge>
{
    public void Configure(EntityTypeBuilder<AccountMerge> builder)
    {
        builder.ToTable("account_merges");
        builder.HasKey(x => x.MergedId);
        builder.HasIndex(x => x.IntoId);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.IntoId).OnDelete(DeleteBehavior.Cascade);
    }
}