using System.Security.Cryptography;
using System.Text;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Secrets;
using HarmonyDB.Shelf.Model.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Auth.Services;

/// <summary>
/// CSRF protection by a signed double-submit cookie bound to the session (OWASP's "signed double-submit cookie"):
/// the cookie <see cref="AuthCookies.CsrfName"/> (<c>__Host-shelf-csrf</c>) holds <c>nonce.HMAC(session, nonce)</c>; a state-changing request
/// sends the same value in the header <see cref="HeaderName"/>. Another site can neither read the cookie nor make a
/// valid value for the victim's session. The cookie is given (and given anew when it no longer fits the session — after
/// signing in or out) on every answer of <c>/api</c>.
/// <para>
/// Checked on every POST, PUT, PATCH and DELETE under <c>/api</c> that comes with a valid session (where the browser's
/// cookie would act for the person), and on the endpoints of signing in (<see cref="RequireCsrfAttribute"/>) for
/// guests too, so that nobody can be signed in to someone else's account. A request without a session carries no
/// authority to abuse.
/// </para>
/// </summary>
public sealed class CsrfTokens(IOptionsMonitor<SecretsOptions> secrets)
{
    public const string HeaderName = "X-CSRF-Token";

    private const string GuestBinding = "guest";
    private const string SessionBindingItem = "shelf.csrf.session";
    private const string FinalBindingItem = "shelf.csrf.final";

    /// <summary>
    /// Called by the session's authentication: this request comes with a valid session.
    /// </summary>
    public static void SetSession(HttpContext context, byte[] sessionTokenHash)
        => context.Items[SessionBindingItem] = Convert.ToBase64String(sessionTokenHash);

    /// <summary>
    /// Called when this request signs in or out: the token given with its answer is for the new state.
    /// </summary>
    public static void SetAnswerSession(HttpContext context, byte[]? sessionTokenHash)
        => context.Items[FinalBindingItem] = sessionTokenHash == null ? GuestBinding : Convert.ToBase64String(sessionTokenHash);

    public static bool HasSession(HttpContext context) => context.Items.ContainsKey(SessionBindingItem);

    public string Create(string binding)
    {
        var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
        return $"{nonce}.{Sign(binding, nonce)}";
    }

    public bool IsValid(string? token, string binding)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        var dot = token.IndexOf('.');
        if (dot <= 0)
            return false;

        var expected = Encoding.ASCII.GetBytes(Sign(binding, token[..dot]));
        var given = Encoding.ASCII.GetBytes(token[(dot + 1)..]);
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }

    /// <summary>
    /// The middleware of the area <c>auth</c>.
    /// </summary>
    internal static async Task MiddlewareAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var tokens = context.RequestServices.GetRequiredService<CsrfTokens>();
        var cookies = context.RequestServices.GetRequiredService<AuthCookies>();
        var cookie = context.Request.Cookies[cookies.CsrfName];
        var requestBinding = context.Items[SessionBindingItem] as string ?? GuestBinding;

        context.Response.OnStarting(() =>
        {
            var answerBinding = context.Items[FinalBindingItem] as string ?? requestBinding;
            if (!tokens.IsValid(cookie, answerBinding))
                context.Response.Cookies.Append(cookies.CsrfName, tokens.Create(answerBinding), cookies.Csrf());

            if (SessionAuthenticationHandler.IsStale(context) && !context.Items.ContainsKey(FinalBindingItem))
                context.Response.Cookies.Delete(cookies.SessionName, cookies.Expired(true));

            return Task.CompletedTask;
        });

        var unsafeMethod = !(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method));
        var required = HasSession(context) || context.GetEndpoint()?.Metadata.GetMetadata<RequireCsrfAttribute>() != null;
        if (unsafeMethod && required)
        {
            var header = context.Request.Headers[HeaderName].ToString();
            if (header != cookie || !tokens.IsValid(header, requestBinding))
            {
                await ErrorResponses.WriteAsync(context, StatusCodes.Status403Forbidden, AuthErrorCodes.CsrfFailed);
                return;
            }
        }

        await next(context);
    }

    private string Sign(string binding, string nonce)
    {
        var options = secrets.CurrentValue;
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, options.GetKey(options.KeyVersion), 32, info: "HarmonyDB.Shelf CSRF"u8.ToArray());
        return WebEncoders.Base64UrlEncode(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{binding}\n{nonce}")));
    }
}

/// <summary>
/// The endpoint checks the CSRF token for guests too (the endpoints of signing in).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireCsrfAttribute : Attribute;
