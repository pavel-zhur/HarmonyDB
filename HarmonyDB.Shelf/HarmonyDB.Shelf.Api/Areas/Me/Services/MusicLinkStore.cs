using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Me;
using Microsoft.EntityFrameworkCore;
using MusicService = HarmonyDB.Shelf.Database.Model.MusicService;

namespace HarmonyDB.Shelf.Api.Areas.Me.Services;

/// <summary>
/// A person's linked music accounts: what they copied from the browser, kept encrypted (<see cref="MusicLinkCipher"/>),
/// never given back to the browser. The link's id is the subject the music service keeps the person's answers under;
/// removing a link purges them first (<see cref="AudioClient.PurgeLinksAsync"/>), then removes it: when the music
/// service does not do it, the link stays and the act answers 503 <c>unavailable</c>. What is kept under the link in
/// the moment between the purge and the removal stays at the music service.
/// <para>
/// For the areas that act with the person's own music account: <see cref="GetActingAsync"/>.
/// </para>
/// </summary>
public sealed class MusicLinkStore(ShelfDbContext db, MusicLinkCipher cipher, AudioClient audio, TimeProvider time)
{
    /// <summary>
    /// The most credentials may take, in UTF-8 bytes: they travel to the music service in a request header (base64),
    /// and this size passes it with room to spare. YouTube Music's request headers are a few kilobytes.
    /// </summary>
    public const int MaxCredentialsBytes = 48 * 1024;

    public async Task<IReadOnlyList<MusicLinkItem>> ListAsync(Guid accountId, CancellationToken cancellationToken)
        => (await db.MusicLinks.AsNoTracking().Where(x => x.AccountId == accountId).OrderBy(x => x.Service).ToListAsync(cancellationToken))
            .Select(x => new MusicLinkItem
            {
                Service = ToModel(x.Service),
                ExternalName = x.ExternalName,
                CreatedAt = x.CreatedAt,
                LastUsedAt = x.LastUsedAt,
            })
            .ToList();

    /// <summary>
    /// Links the music account (in place of the one linked before for the service), after the music service has said
    /// the credentials work.
    /// </summary>
    public async Task<MusicLinkItem> PutAsync(Guid accountId, Model.Me.MusicService service, string credentials, CancellationToken cancellationToken)
    {
        credentials = credentials.Trim();
        if (credentials.Length == 0)
            throw ShelfException.BadRequest(MeErrorCodes.MusicCredentialsRejected);
        if (System.Text.Encoding.UTF8.GetByteCount(credentials) > MaxCredentialsBytes)
        {
            throw ShelfException.BadRequest(
                MeErrorCodes.MusicCredentialsTooLarge,
                new Dictionary<string, string> { ["maxBytes"] = MaxCredentialsBytes.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }

        var now = time.GetUtcNow();
        var link = new MusicLink
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Service = ToDatabase(service),
            CredentialKind = service == Model.Me.MusicService.Yandex ? MusicCredentialKind.Token : MusicCredentialKind.Headers,
            Ciphertext = [],
            Nonce = [],
            Tag = [],
            CreatedAt = now,
        };

        link.ExternalName = await CheckAsync(link, credentials, cancellationToken);
        cipher.Encrypt(link, credentials);

        var replaced = await db.MusicLinks.SingleOrDefaultAsync(x => x.AccountId == accountId && x.Service == link.Service, cancellationToken);
        if (replaced != null)
            await audio.PurgeLinksAsync([replaced.Id], cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (replaced != null)
        {
            db.MusicLinks.Remove(replaced);
            await db.SaveChangesAsync(cancellationToken);
        }

        db.MusicLinks.Add(link);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new() { Service = service, ExternalName = link.ExternalName, CreatedAt = now };
    }

    public async Task DeleteAsync(Guid accountId, Model.Me.MusicService service, CancellationToken cancellationToken)
    {
        var databaseService = ToDatabase(service);
        var link = await db.MusicLinks.SingleOrDefaultAsync(x => x.AccountId == accountId && x.Service == databaseService, cancellationToken)
                   ?? throw ShelfException.NotFound(MeErrorCodes.NoSuchMusicLink);

        await audio.PurgeLinksAsync([link.Id], cancellationToken);
        db.MusicLinks.Remove(link);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// How to call the music service as the person with their own linked account of the service; null when none is
    /// linked. A link still under an old key is encrypted anew under the current one.
    /// </summary>
    public async Task<AudioActing?> GetActingAsync(Guid accountId, Model.Me.MusicService service, CancellationToken cancellationToken)
    {
        var databaseService = ToDatabase(service);
        var link = await db.MusicLinks.SingleOrDefaultAsync(x => x.AccountId == accountId && x.Service == databaseService, cancellationToken);
        if (link == null)
            return null;

        var credentials = cipher.Decrypt(link);
        if (cipher.IsUnderOldKey(link))
            cipher.Encrypt(link, credentials);

        link.LastUsedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return AudioActing.Person(link.Id.ToString(), credentials);
    }

    /// <summary>
    /// Asks the music service who the credentials are; the account's name there.
    /// </summary>
    private async Task<string?> CheckAsync(MusicLink link, string credentials, CancellationToken cancellationToken)
    {
        AudioWho who;
        try
        {
            who = await audio.GetMeAsync(ToAudio(link.Service), AudioActing.Person(link.Id.ToString(), credentials), cancellationToken);
        }
        catch (AudioException exception) when (exception.StatusCode is StatusCodes.Status400BadRequest or StatusCodes.Status403Forbidden
                                               && exception.Code is "credentials_invalid" or "credentials_rejected" or "account_required")
        {
            throw ShelfException.BadRequest(MeErrorCodes.MusicCredentialsRejected);
        }
        catch (AudioException)
        {
            throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
        }

        if (!who.Authed)
            throw ShelfException.BadRequest(MeErrorCodes.MusicCredentialsRejected);

        return who.Account;
    }

    public static MusicService ToDatabase(Model.Me.MusicService service) => service switch
    {
        Model.Me.MusicService.Yandex => MusicService.Yandex,
        Model.Me.MusicService.Youtube => MusicService.Youtube,
        _ => throw new ArgumentOutOfRangeException(nameof(service)),
    };

    private static Model.Me.MusicService ToModel(MusicService service) => service switch
    {
        MusicService.Yandex => Model.Me.MusicService.Yandex,
        MusicService.Youtube => Model.Me.MusicService.Youtube,
        _ => throw new ArgumentOutOfRangeException(nameof(service)),
    };

    private static AudioServiceName ToAudio(MusicService service) => service switch
    {
        MusicService.Yandex => AudioServiceName.Yandex,
        MusicService.Youtube => AudioServiceName.Youtube,
        _ => throw new ArgumentOutOfRangeException(nameof(service)),
    };
}
