using HarmonyDB.Shelf.Api.Areas.Auth.Services.Providers;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Auth.Services;

/// <summary>
/// A sign-in, from its start to its end, for every provider the same way.
/// <list type="number">
/// <item>Start (a POST with the CSRF token): a flow is kept with a random state (and, for a provider that sends the
/// person away, a PKCE verifier and a nonce); the state goes into an HttpOnly cookie of this browser. A flow that adds
/// a sign-in to the account (<see cref="SignInPurpose.Link"/>) is bound to the session that started it.</item>
/// <item>The provider's answer (its redirect back, or a POST from the app) must come with the same state in the cookie,
/// for the same provider, before the flow expires, once; a linking flow only in the session that started it.</item>
/// <item>The provider checks the answer and gives the identity. Signing in: the identity's account (made now, at the
/// first sign-in), and a new session token. Linking: the identity is added to the session's account; when it is
/// another account's, the person is asked, and a merge may follow (<see cref="MergeAsync"/>).</item>
/// </list>
/// </summary>
public sealed class SignInFlowService(
    ShelfDbContext db,
    IEnumerable<ISignInProvider> providers,
    SessionService sessions,
    AccountMerger merger,
    PermissionCatalog permissions,
    AuthCookies cookies,
    IOptionsMonitor<SignInOptions> options,
    TimeProvider time,
    ILogger<SignInFlowService> logger)
{
    public async Task<SignInStart> StartAsync(HttpContext context, StartSignInRequest request, CancellationToken cancellationToken)
    {
        var provider = FindProvider(request.Provider);
        var returnPath = request.ReturnPath ?? "/";
        if (!IsAppPath(returnPath))
            throw ShelfException.BadRequest(AuthErrorCodes.ReturnPathInvalid);

        var now = time.GetUtcNow();
        Guid? sessionId = null;
        if (request.Purpose == SignInPurpose.Link)
        {
            sessionId = SessionService.GetSessionId(context.User);
            if (sessionId == null || context.User.IsDelegated())
                throw new ShelfException(StatusCodes.Status401Unauthorized, ErrorCodes.SignInRequired);

            // A session taken over must not attach its own identity: adding a sign-in needs a recent sign-in.
            if (!SessionService.IsSignedInSince(context.User, now - options.CurrentValue.FreshSignIn))
                throw ShelfException.Forbidden(AuthErrorCodes.FreshSignInRequired);
        }

        var lifetime = options.CurrentValue.FlowLifetime;
        var flow = new SignInFlow
        {
            State = AuthTokens.NewToken(),
            Provider = provider.Name,
            Purpose = request.Purpose == SignInPurpose.Link ? SignInFlowPurpose.Link : SignInFlowPurpose.SignIn,
            SessionId = sessionId,
            CodeVerifier = provider.SendsAway ? AuthTokens.NewToken() : null,
            Nonce = provider.SendsAway ? AuthTokens.NewToken() : null,
            ReturnPath = returnPath,
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        };

        var redirectUrl = provider.SendsAway ? await provider.GetAuthorizationUrlAsync(flow, cancellationToken) : null;

        // Flows past their time are not kept.
        await db.SignInFlows.Where(x => x.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
        db.SignInFlows.Add(flow);
        await db.SaveChangesAsync(cancellationToken);

        context.Response.Cookies.Append(cookies.FlowName, flow.State, cookies.Flow(lifetime));
        return new() { State = flow.State, RedirectUrl = redirectUrl };
    }

    /// <summary>
    /// The answer of a provider that is answered in the app.
    /// </summary>
    public async Task<SignInResult> CompleteAsync(HttpContext context, string state, IReadOnlyDictionary<string, string> answer, CancellationToken cancellationToken)
    {
        var flow = await FindOpenFlowAsync(context, state, null, cancellationToken);
        var provider = FindProvider(flow.Provider);
        if (provider.SendsAway)
            throw ShelfException.BadRequest(AuthErrorCodes.FlowInvalid);

        return await FinishAsync(context, flow, await provider.CompleteAsync(flow, answer, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// The redirect back from a provider's site: where to send the browser on. The outcome (or the error's code) and
    /// the state go in the query: <c>auth</c>, <c>auth_flow</c>.
    /// </summary>
    public async Task<string> CallbackAsync(HttpContext context, string providerName, IQueryCollection query, CancellationToken cancellationToken)
    {
        var state = query["state"].ToString();
        var returnPath = "/";
        try
        {
            var flow = await FindOpenFlowAsync(context, state, providerName, cancellationToken);
            returnPath = flow.ReturnPath ?? "/";
            var provider = FindProvider(flow.Provider);
            if (!provider.SendsAway)
                throw ShelfException.BadRequest(AuthErrorCodes.FlowInvalid);

            var answer = query.ToDictionary(x => x.Key, x => x.Value.ToString());
            var result = await FinishAsync(context, flow, await provider.CompleteAsync(flow, answer, cancellationToken), cancellationToken);
            return WithQuery(returnPath, ToCode(result.Outcome), result.Outcome == SignInOutcome.MergeNeeded ? state : null);
        }
        catch (ShelfException exception)
        {
            if (exception is SignInProviderException providerException)
                logger.LogWarning("A sign-in by {Provider} failed: {Reason}", providerName, providerException.Reason);

            context.Response.Cookies.Delete(cookies.FlowName, cookies.Expired(true));
            return WithQuery(returnPath, exception.Code, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The browser goes back to the app in any case, never to an error page of the API.
            logger.LogError(exception, "A sign-in by {Provider} broke.", providerName);
            context.Response.Cookies.Delete(cookies.FlowName, cookies.Expired(true));
            return WithQuery(returnPath, ErrorCodes.InternalError, null);
        }
    }

    /// <summary>
    /// A linking flow that found the identity in another account: what to ask the person.
    /// </summary>
    public async Task<SignInResult> GetMergeQuestionAsync(HttpContext context, string state, CancellationToken cancellationToken)
    {
        var flow = await FindAnsweredLinkFlowAsync(context, state, cancellationToken);
        return new()
        {
            Outcome = SignInOutcome.MergeNeeded,
            Provider = flow.Provider,
            ReturnPath = flow.ReturnPath ?? "/",
            MergeCandidate = await DescribeAsync(flow.CompletedLogin!.AccountId, cancellationToken),
        };
    }

    /// <summary>
    /// The person agreed: the account the linked identity belongs to is merged into the session's account. Both
    /// identities must have signed in within <see cref="SignInOptions.FreshSignIn"/>: the session's sign-in, and
    /// the flow's answer.
    /// </summary>
    public async Task<SignInResult> MergeAsync(HttpContext context, string state, CancellationToken cancellationToken)
    {
        var flow = await FindAnsweredLinkFlowAsync(context, state, cancellationToken);
        var now = time.GetUtcNow();
        var freshness = options.CurrentValue.FreshSignIn;
        var session = await db.Sessions.AsNoTracking().SingleAsync(x => x.Id == flow.SessionId, cancellationToken);
        if (now - session.SignedInAt > freshness || now - flow.CompletedAt!.Value > freshness)
            throw ShelfException.Forbidden(AuthErrorCodes.FreshSignInRequired);

        var first = session.AccountId;
        var second = flow.CompletedLogin!.AccountId;
        if (second != first)
            await merger.MergeAsync(first, second, cancellationToken);

        db.SignInFlows.Remove(flow);
        await db.SaveChangesAsync(cancellationToken);

        return new() { Outcome = SignInOutcome.Linked, Provider = flow.Provider, ReturnPath = flow.ReturnPath ?? "/" };
    }

    private async Task<SignInResult> FinishAsync(HttpContext context, SignInFlow flow, SignInIdentity identity, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var returnPath = flow.ReturnPath ?? "/";
        context.Response.Cookies.Delete(cookies.FlowName, cookies.Expired(true));

        var login = await db.AccountLogins.Include(x => x.Account)
            .SingleOrDefaultAsync(x => x.Provider == flow.Provider && x.Subject == identity.Subject, cancellationToken);
        if (login != null)
            Refresh(login, identity, now);

        if (flow.Purpose == SignInFlowPurpose.SignIn)
        {
            db.SignInFlows.Remove(flow);
            login ??= await CreateAccountAsync(flow, identity, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await sessions.SignInAsync(context, login.AccountId, cancellationToken);
            return new() { Outcome = SignInOutcome.SignedIn, Provider = flow.Provider, ReturnPath = returnPath };
        }

        // Linking: the flow's session is the request's (FindOpenFlowAsync), so its account is the person's.
        var accountId = context.User.GetAccountId()!.Value;
        if (login == null || login.AccountId == accountId)
        {
            if (login == null)
                db.AccountLogins.Add(NewLogin(accountId, flow.Provider, identity, now));

            db.SignInFlows.Remove(flow);
            await db.SaveChangesAsync(cancellationToken);
            return new() { Outcome = login == null ? SignInOutcome.Linked : SignInOutcome.AlreadyLinked, Provider = flow.Provider, ReturnPath = returnPath };
        }

        // Another account's: ask, and keep the answer for the merge.
        flow.CompletedAt = now;
        flow.CompletedLoginId = login.Id;
        flow.ExpiresAt = now + options.CurrentValue.FreshSignIn;
        await db.SaveChangesAsync(cancellationToken);
        return new()
        {
            Outcome = SignInOutcome.MergeNeeded,
            Provider = flow.Provider,
            ReturnPath = returnPath,
            MergeCandidate = await DescribeAsync(login.AccountId, cancellationToken),
        };
    }

    /// <summary>
    /// A new account with the defaults: its settings, the permissions of a new account; name and picture from the
    /// provider.
    /// </summary>
    private async Task<AccountLogin> CreateAccountAsync(SignInFlow flow, SignInIdentity identity, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var provider = flow.Provider;
        var account = new Account
        {
            Id = Guid.NewGuid(),
            DisplayName = identity.DisplayName,
            PictureUrl = identity.PictureUrl,
            CreatedAt = now,
            LastSeenAt = now,
            Settings = new() { UpdatedAt = now },
            Permissions = permissions.NewAccountDefaults
                .Select(x => new AccountPermission { Permission = x, GrantedAt = now, GrantedBy = "defaults" })
                .ToList(),
        };
        var login = NewLogin(account.Id, provider, identity, now);
        login.Account = account;
        db.Accounts.Add(account);
        db.AccountLogins.Add(login);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return login;
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // The first sign-in of this identity, made at the same moment by another request: its account is the one.
            db.ChangeTracker.Clear();
            await db.SignInFlows.Where(x => x.State == flow.State).ExecuteDeleteAsync(cancellationToken);
            return await db.AccountLogins.SingleAsync(x => x.Provider == provider && x.Subject == identity.Subject, cancellationToken);
        }
    }

    private static AccountLogin NewLogin(Guid accountId, string provider, SignInIdentity identity, DateTimeOffset now) => new()
    {
        AccountId = accountId,
        Provider = provider,
        Subject = identity.Subject,
        DisplayName = identity.DisplayName,
        Username = identity.Username,
        Email = identity.Email,
        PictureUrl = identity.PictureUrl,
        CreatedAt = now,
        LastSignInAt = now,
    };

    /// <summary>
    /// What the provider tells now; the account's name and picture follow the provider signed in with last.
    /// </summary>
    private static void Refresh(AccountLogin login, SignInIdentity identity, DateTimeOffset now)
    {
        login.DisplayName = identity.DisplayName;
        login.Username = identity.Username;
        login.Email = identity.Email;
        login.PictureUrl = identity.PictureUrl;
        login.LastSignInAt = now;
        login.Account.DisplayName = identity.DisplayName;
        login.Account.PictureUrl = identity.PictureUrl;
    }

    private async Task<MergeCandidate> DescribeAsync(Guid accountId, CancellationToken cancellationToken)
        => await db.Accounts.AsNoTracking().Where(x => x.Id == accountId)
            .Select(x => new MergeCandidate
            {
                DisplayName = x.DisplayName,
                PictureUrl = x.PictureUrl,
                CreatedAt = x.CreatedAt,
                Providers = x.Logins.OrderBy(l => l.CreatedAt).Select(l => l.Provider).ToList(),
            })
            .SingleAsync(cancellationToken);

    /// <summary>
    /// A flow not answered yet, of this browser (the state cookie), not expired; a linking one in its own session.
    /// </summary>
    private async Task<SignInFlow> FindOpenFlowAsync(HttpContext context, string state, string? providerName, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var flow = string.IsNullOrEmpty(state) || context.Request.Cookies[cookies.FlowName] != state
            ? null
            : await db.SignInFlows.SingleOrDefaultAsync(x => x.State == state, cancellationToken);

        if (flow == null || flow.ExpiresAt <= now || flow.CompletedAt != null || (providerName != null && flow.Provider != providerName)
            || (flow.Purpose == SignInFlowPurpose.Link && flow.SessionId != SessionService.GetSessionId(context.User)))
        {
            throw ShelfException.BadRequest(AuthErrorCodes.FlowInvalid);
        }

        return flow;
    }

    /// <summary>
    /// A linking flow of the request's session, answered with another account's identity, not expired.
    /// </summary>
    private async Task<SignInFlow> FindAnsweredLinkFlowAsync(HttpContext context, string state, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var sessionId = SessionService.GetSessionId(context.User);
        var flow = await db.SignInFlows.Include(x => x.CompletedLogin)
            .SingleOrDefaultAsync(x => x.State == state, cancellationToken);

        if (flow == null || sessionId == null || flow.SessionId != sessionId || flow.Purpose != SignInFlowPurpose.Link
            || flow.CompletedLogin == null || flow.ExpiresAt <= now)
        {
            throw ShelfException.BadRequest(AuthErrorCodes.FlowInvalid);
        }

        return flow;
    }

    private ISignInProvider FindProvider(string name)
        => providers.SingleOrDefault(x => x.Name == name && x.IsEnabled) ?? throw ShelfException.NotFound(AuthErrorCodes.UnknownProvider);

    /// <summary>
    /// A path of this app: one "/" at the start (not "//", not "/\": those leave the site), no control characters.
    /// </summary>
    internal static bool IsAppPath(string path)
        => path.Length is > 0 and <= 512
           && path[0] == '/'
           && (path.Length == 1 || (path[1] != '/' && path[1] != '\\'))
           && !path.Any(char.IsControl);

    private static string WithQuery(string path, string code, string? state)
    {
        var query = new Dictionary<string, string?> { ["auth"] = code };
        if (state != null)
            query["auth_flow"] = state;

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(path, query);
    }

    private static string ToCode(SignInOutcome outcome) => outcome switch
    {
        SignInOutcome.SignedIn => "signed_in",
        SignInOutcome.Linked => "linked",
        SignInOutcome.AlreadyLinked => "already_linked",
        SignInOutcome.MergeNeeded => "merge_needed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };
}
