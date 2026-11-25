using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Auth;

namespace HarmonyDB.Shelf.Api.Areas.Auth.Services.Providers;

/// <summary>
/// A way of signing in. A provider is one class registered in <see cref="AuthArea"/>; nothing else knows it.
/// <para>
/// Either the person signs in on the provider's site (<see cref="SendsAway"/>: the browser goes to
/// <see cref="GetAuthorizationUrlAsync"/> and comes back to <c>/api/auth/callback/{name}</c> with the provider's answer
/// in the query), or the answer is got in the app and posted to <c>/api/auth/flows/{state}/complete</c>. Either way the
/// answer goes to <see cref="CompleteAsync"/>, which checks it and gives the identity, or throws
/// <see cref="SignInProviderException"/>.
/// </para>
/// </summary>
public interface ISignInProvider
{
    /// <summary>
    /// The name (<see cref="Foundation.SignIn.SignInProviders"/>), as kept in <c>account_logins.provider</c>.
    /// </summary>
    string Name { get; }

    bool IsEnabled { get; }

    bool SendsAway { get; }

    /// <summary>
    /// For a provider that <see cref="SendsAway"/>: where to send the browser for this sign-in (its state, PKCE
    /// verifier and nonce are in the flow).
    /// </summary>
    Task<string> GetAuthorizationUrlAsync(SignInFlow flow, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the provider's answer for this sign-in and gives the identity it proves.
    /// </summary>
    Task<SignInIdentity> CompleteAsync(SignInFlow flow, IReadOnlyDictionary<string, string> answer, CancellationToken cancellationToken);
}

/// <summary>
/// An identity a provider proved: its stable id there and what it tells about the person.
/// </summary>
public sealed record SignInIdentity(string Subject, string DisplayName)
{
    public string? Username { get; init; }

    public string? Email { get; init; }

    public string? PictureUrl { get; init; }
}

/// <summary>
/// The provider's answer did not prove an identity.
/// </summary>
public sealed class SignInProviderException(int statusCode, string code, string reason)
    : ShelfException(statusCode, code)
{
    /// <summary>
    /// For the log.
    /// </summary>
    public string Reason { get; } = reason;

    public static SignInProviderException Invalid(string reason) => new(StatusCodes.Status400BadRequest, AuthErrorCodes.ProviderAnswerInvalid, reason);

    public static SignInProviderException Refused(string reason) => new(StatusCodes.Status400BadRequest, AuthErrorCodes.ProviderRefused, reason);

    public static SignInProviderException Unavailable(string reason) => new(StatusCodes.Status503ServiceUnavailable, AuthErrorCodes.ProviderUnavailable, reason);
}
