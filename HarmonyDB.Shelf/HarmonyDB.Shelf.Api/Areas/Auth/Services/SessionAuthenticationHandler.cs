using System.Security.Claims;
using System.Text.Encodings.Web;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using HarmonyDB.Shelf.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Auth.Services;

/// <summary>
/// The browser's sign-in: the session cookie's token, looked up by its hash. A session that is not found, has ended
/// (idle or absolute lifetime) or whose account is deleted gives no identity (the request is a guest's) and its cookie
/// is deleted with the answer. The identity carries the account's permissions as stored now.
/// </summary>
public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ShelfDbContext db,
    AuthCookies cookies,
    IOptionsMonitor<SignInOptions> signIn,
    TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    public const string SchemeName = "Session";

    /// <summary>
    /// The claim with the session's id.
    /// </summary>
    public const string SessionClaimType = "shelf_session";

    /// <summary>
    /// The claim with the time of the session's last sign-in (Unix milliseconds).
    /// </summary>
    public const string SignedInClaimType = "shelf_signed_in";

    private const string StaleItem = "shelf.session.stale";

    /// <summary>
    /// How often a session's "last seen" is written at most.
    /// </summary>
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

    public static bool IsStale(HttpContext context) => context.Items.ContainsKey(StaleItem);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Cookies[cookies.SessionName];
        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        var hash = AuthTokens.Hash(token);
        var now = time.GetUtcNow();
        var session = await db.Sessions
            .Where(x => x.TokenHash == hash && x.ExpiresAt > now && x.Account.DeletedAt == null)
            .Select(x => new
            {
                x.Id,
                x.AccountId,
                x.SignedInAt,
                x.LastSeenAt,
                Permissions = x.Account.Permissions.Select(p => p.Permission).ToList(),
            })
            .SingleOrDefaultAsync(Context.RequestAborted);

        if (session == null)
        {
            Context.Items[StaleItem] = true;
            return AuthenticateResult.NoResult();
        }

        if (now - session.LastSeenAt >= TouchInterval)
        {
            var lifetimes = signIn.CurrentValue.Sessions;
            var expiresAt = Min(session.SignedInAt + lifetimes.AbsoluteLifetime, now + lifetimes.IdleTimeout);
            await db.Sessions.Where(x => x.Id == session.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.LastSeenAt, now).SetProperty(s => s.ExpiresAt, expiresAt), Context.RequestAborted);
            await db.Accounts.Where(x => x.Id == session.AccountId)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.LastSeenAt, now), Context.RequestAborted);
        }

        var subjects = await AccountSubjects.LoadAsync(db, session.AccountId, Context.RequestAborted);
        var identity = ShelfIdentity.Create(SchemeName, session.AccountId, subjects, session.Permissions, null);
        identity.AddClaim(new(SessionClaimType, session.Id.ToString()));
        identity.AddClaim(new(SignedInClaimType, session.SignedInAt.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
        CsrfTokens.SetSession(Context, hash);

        return AuthenticateResult.Success(new(new ClaimsPrincipal(identity), SchemeName));
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
