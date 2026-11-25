using System.Text.RegularExpressions;
using HarmonyDB.Shelf.Api.Foundation.SignIn;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Auth.Services.Providers;

/// <summary>
/// Signs in as a named test account, or as an existing account, with no check: it is how the local stack is walked.
/// It exists only when the app runs in the Development environment and <c>SignIn:Dev:Enabled</c> is on.
/// </summary>
public sealed partial class DevSignInProvider(IOptionsMonitor<SignInOptions> options, IHostEnvironment environment) : ISignInProvider
{
    public string Name => SignInProviders.Dev;

    public bool IsEnabled => options.CurrentValue.Dev.Enabled && environment.IsDevelopment();

    public bool SendsAway => false;

    public Task<string> GetAuthorizationUrlAsync(SignInFlow flow, CancellationToken cancellationToken)
        => throw new NotSupportedException("The dev sign-in is answered in the app.");

    /// <summary>
    /// The answer, one of: <c>name</c> — a test account's name (letters, digits, "-", "_"; up to 64), made at its first
    /// sign-in; <c>accountId</c> — an existing account (an imported person's, say), signed in as it is: nothing in it
    /// changes and no sign-in is added to it.
    /// </summary>
    public Task<SignInIdentity> CompleteAsync(SignInFlow flow, IReadOnlyDictionary<string, string> answer, CancellationToken cancellationToken)
    {
        var hasName = answer.TryGetValue("name", out var name);
        var hasAccount = answer.TryGetValue("accountId", out var accountText);
        if (hasName == hasAccount)
            throw SignInProviderException.Invalid("Dev: give either a name or an account id.");

        if (hasAccount)
        {
            return Guid.TryParse(accountText, out var accountId)
                ? Task.FromResult(SignInIdentity.ExistingAccount(accountId))
                : throw SignInProviderException.Invalid("Dev: the account id is not a uuid.");
        }

        if (!NamePattern().IsMatch(name!))
            throw SignInProviderException.Invalid("Dev: the name is not allowed.");

        return Task.FromResult(new SignInIdentity(name!, name!) { Username = name });
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex NamePattern();
}
