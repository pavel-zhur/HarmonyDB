using System.Security.Claims;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Auth.Services;

/// <summary>
/// The browser's sessions: made at sign-in, ended at sign-out or revoked. The browser holds an opaque token; the
/// database keeps its hash.
/// </summary>
public sealed class SessionService(
    ShelfDbContext db,
    AuthCookies cookies,
    IOptionsMonitor<SignInOptions> options,
    TimeProvider time)
{
    /// <summary>
    /// The session of the request, if it came with a valid one.
    /// </summary>
    public static Guid? GetSessionId(ClaimsPrincipal user)
        => user.FindFirst(SessionAuthenticationHandler.SessionClaimType) is { } claim ? Guid.Parse(claim.Value) : null;

    /// <summary>
    /// Whether the request's own session signed in at or after the time (a recent sign-in, asked before changing the
    /// account's sign-ins).
    /// </summary>
    public static bool IsSignedInSince(ClaimsPrincipal user, DateTimeOffset since)
        => user.FindFirst(SessionAuthenticationHandler.SignedInClaimType) is { } claim
           && DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(claim.Value, System.Globalization.CultureInfo.InvariantCulture)) >= since;

    /// <summary>
    /// Signs the browser in to the account, always with a new token (rotation): when the browser's session is this
    /// account's, the same session gets the new token and its sign-in time (sign-ins under way bound to it stay
    /// bound); otherwise the browser's session, if any, ends and a new one begins. Saves the changes of the context.
    /// </summary>
    public async Task SignInAsync(HttpContext context, Guid accountId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var lifetimes = options.CurrentValue.Sessions;
        var token = AuthTokens.NewToken();
        var hash = AuthTokens.Hash(token);

        var currentId = GetSessionId(context.User);
        var session = currentId != null && context.User.GetAccountId() == accountId
            ? await db.Sessions.SingleOrDefaultAsync(x => x.Id == currentId, cancellationToken)
            : null;

        if (session == null)
        {
            if (currentId != null)
                await db.Sessions.Where(x => x.Id == currentId).ExecuteDeleteAsync(cancellationToken);

            session = new()
            {
                TokenHash = hash,
                AccountId = accountId,
                CreatedAt = now,
                UserAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 512),
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            };
            db.Sessions.Add(session);
        }

        session.TokenHash = hash;
        session.SignedInAt = now;
        session.LastSeenAt = now;
        session.ExpiresAt = now + lifetimes.IdleTimeout < now + lifetimes.AbsoluteLifetime ? now + lifetimes.IdleTimeout : now + lifetimes.AbsoluteLifetime;

        var account = await db.Accounts.SingleAsync(x => x.Id == accountId, cancellationToken);
        account.LastSeenAt = now;

        // Ended sessions are not kept.
        await db.Sessions.Where(x => x.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        context.Response.Cookies.Append(cookies.SessionName, token, cookies.Session(lifetimes.AbsoluteLifetime));
        CsrfTokens.SetAnswerSession(context, hash);
    }

    /// <summary>
    /// Ends the browser's session.
    /// </summary>
    public async Task SignOutAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (GetSessionId(context.User) is { } id)
            await db.Sessions.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);

        ForgetInBrowser(context);
    }

    /// <summary>
    /// Deletes the browser's session cookie (its session ended some other way).
    /// </summary>
    public void ForgetInBrowser(HttpContext context)
    {
        context.Response.Cookies.Delete(cookies.SessionName, cookies.Expired(true));
        CsrfTokens.SetAnswerSession(context, null);
    }

    public async Task<IReadOnlyList<Session>> ListAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return await db.Sessions.AsNoTracking()
            .Where(x => x.AccountId == accountId && x.ExpiresAt > now)
            .OrderByDescending(x => x.LastSeenAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Ends one session of the account. False when there is no such session.
    /// </summary>
    public async Task<bool> RevokeAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken)
        => await db.Sessions.Where(x => x.AccountId == accountId && x.Id == sessionId).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>
    /// Ends every session of the account but one.
    /// </summary>
    public Task<int> RevokeOthersAsync(Guid accountId, Guid keep, CancellationToken cancellationToken)
        => db.Sessions.Where(x => x.AccountId == accountId && x.Id != keep).ExecuteDeleteAsync(cancellationToken);

    private static string? Truncate(string text, int length)
        => string.IsNullOrEmpty(text) ? null : text.Length <= length ? text : text[..length];
}
