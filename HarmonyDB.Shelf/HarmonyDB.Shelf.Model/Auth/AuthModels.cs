namespace HarmonyDB.Shelf.Model.Auth;

public enum SignInPurpose
{
    /// <summary>Sign in (an account is made at the first sign-in).</summary>
    SignIn,

    /// <summary>Add this sign-in to the account of the current session.</summary>
    Link,
}

/// <summary>
/// Starts a sign-in at a provider.
/// </summary>
public record StartSignInRequest
{
    /// <summary>
    /// "telegram", "google", "yandex", "dev": one of the providers the public configuration lists.
    /// </summary>
    public required string Provider { get; init; }

    public SignInPurpose Purpose { get; init; } = SignInPurpose.SignIn;

    /// <summary>
    /// Where in the app to come back to (a path of the app, starting with one "/"). Default "/".
    /// </summary>
    public string? ReturnPath { get; init; }
}

public record SignInStart
{
    /// <summary>
    /// The sign-in under way; given back with its answer.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// For the providers that sign in on their own site (telegram, google, yandex): the address to send the browser to. It comes
    /// back to the app at the return path with <c>auth</c> (a <see cref="SignInOutcome"/> or an error code) and
    /// <c>auth_flow</c> (the state) in the query. Null for the provider answered in the app (dev):
    /// its answer is posted to <c>/api/auth/flows/{state}/complete</c>.
    /// </summary>
    public string? RedirectUrl { get; init; }
}

/// <summary>
/// The answer of a provider that is answered in the app.
/// </summary>
public record CompleteSignInRequest
{
    /// <summary>
    /// dev: <c>name</c>, a test account's name, or <c>accountId</c>, an existing account to sign in as.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Answer { get; init; }
}

public enum SignInOutcome
{
    /// <summary>Signed in; the session is new.</summary>
    SignedIn,

    /// <summary>The sign-in was added to the account.</summary>
    Linked,

    /// <summary>The sign-in was already this account's.</summary>
    AlreadyLinked,

    /// <summary>
    /// The sign-in has an account of its own. Ask the person; on consent, post
    /// <c>/api/auth/flows/{state}/merge</c> (within the time a merge allows).
    /// </summary>
    MergeNeeded,
}

public record SignInResult
{
    public required SignInOutcome Outcome { get; init; }

    public required string Provider { get; init; }

    /// <summary>
    /// Where in the app to go on.
    /// </summary>
    public required string ReturnPath { get; init; }

    /// <summary>
    /// For <see cref="SignInOutcome.MergeNeeded"/>: the account the sign-in belongs to now.
    /// </summary>
    public MergeCandidate? MergeCandidate { get; init; }
}

/// <summary>
/// The other account, shown when the person is asked whether to merge it into theirs.
/// </summary>
public record MergeCandidate
{
    public required string DisplayName { get; init; }

    public string? PictureUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The other account's sign-ins ("telegram", "google" …), each moved over by the merge.
    /// </summary>
    public required IReadOnlyList<string> Providers { get; init; }
}

/// <summary>
/// Internal: an account's identities at one provider (their stable ids there), oldest first.
/// </summary>
public record AccountLoginSubjects
{
    public required IReadOnlyList<string> Subjects { get; init; }
}

/// <summary>
/// Who the browser is.
/// </summary>
public record CurrentSession
{
    /// <summary>
    /// Null for a guest.
    /// </summary>
    public Guid? AccountId { get; init; }

    /// <summary>
    /// The built-in permissions in force (a guest's defaults for a guest).
    /// </summary>
    public required IReadOnlyList<string> Permissions { get; init; }
}
