using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Auth;

namespace HarmonyDB.Shelf.Tests.Auth;

/// <summary>
/// A browser of the app, as the frontend would act: it keeps the cookies it is given, and sends the CSRF cookie's
/// value as <c>X-CSRF-Token</c> with every state-changing request (unless told not to).
/// </summary>
public sealed class Browser(HttpClient client)
{
    public static readonly JsonSerializerOptions Json = CreateJson();

    public Dictionary<string, string> Cookies { get; } = new();

    /// <summary>
    /// The CSRF cookie's value (<c>__Host-shelf-csrf</c> on https, <c>shelf-csrf</c> on the local stack's http).
    /// </summary>
    public string? CsrfToken => Cookies.FirstOrDefault(x => x.Key.EndsWith("shelf-csrf", StringComparison.Ordinal)).Value;

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, bool csrf = true)
    {
        var request = new HttpRequestMessage(method, path);
        if (body != null)
            request.Content = JsonContent.Create(body, options: Json);
        if (Cookies.Count > 0)
            request.Headers.Add("Cookie", string.Join("; ", Cookies.Select(x => $"{x.Key}={x.Value}")));
        if (csrf && method != HttpMethod.Get && CsrfToken is { } token)
            request.Headers.Add("X-CSRF-Token", token);

        var response = await client.SendAsync(request);
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var setCookie in setCookies)
            {
                var pair = setCookie.Split(';')[0];
                var name = pair[..pair.IndexOf('=')];
                var value = pair[(pair.IndexOf('=') + 1)..];
                if (value.Length == 0 || setCookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase))
                    Cookies.Remove(name);
                else
                    Cookies[name] = value;
            }
        }

        return response;
    }

    public async Task<T> GetAsync<T>(string path)
    {
        var response = await SendAsync(HttpMethod.Get, path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    /// <summary>
    /// Signs in (or links, when <paramref name="purpose"/> says) with the dev provider as the named test account.
    /// </summary>
    public Task<SignInResult> DevAsync(string name, SignInPurpose purpose = SignInPurpose.SignIn)
        => DevAnswerAsync(new Dictionary<string, string> { ["name"] = name }, purpose);

    /// <summary>
    /// Signs in with the dev provider as an existing account.
    /// </summary>
    public Task<SignInResult> DevAsAccountAsync(Guid accountId)
        => DevAnswerAsync(new Dictionary<string, string> { ["accountId"] = accountId.ToString() }, SignInPurpose.SignIn);

    private async Task<SignInResult> DevAnswerAsync(Dictionary<string, string> answer, SignInPurpose purpose)
    {
        if (CsrfToken == null)
            await SendAsync(HttpMethod.Get, "/api/app/configuration");

        var start = await SendAsync(HttpMethod.Post, "/api/auth/flows", new StartSignInRequest { Provider = "dev", Purpose = purpose });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var state = (await start.Content.ReadFromJsonAsync<SignInStart>(Json))!.State;

        var complete = await SendAsync(HttpMethod.Post, $"/api/auth/flows/{state}/complete", new CompleteSignInRequest { Answer = answer });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        return (await complete.Content.ReadFromJsonAsync<SignInResult>(Json))!;
    }

    public static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<ErrorResponse>(Json))!.Code);
    }

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        FoundationExtensions.ConfigureJson(options);

        // the jobs' results, as the frontend's client knows them, and the tests' own job's
        new JobResults(new (string Kind, Type Result)[]
        {
            (global::HarmonyDB.Shelf.Api.Areas.Book.Services.BookService.PagesJobKind, typeof(Model.Book.BookPagesResult)),
            (global::HarmonyDB.Shelf.Api.Areas.Book.Services.BookService.BookJobKind, typeof(Model.Book.BookResult)),
            (global::HarmonyDB.Shelf.Api.Areas.Illustrations.Services.IllustrationsService.JobKind, typeof(Model.Illustrations.IllustrationsJobResult)),
            (global::HarmonyDB.Shelf.Api.Areas.Import.Services.ImportService.JobKind, typeof(Model.Import.ImportResult)),
            (global::HarmonyDB.Shelf.Api.Areas.Playlists.Services.PlaylistService.SyncJobKind, typeof(Model.Playlists.PlaylistSyncResult)),
            (Jobs.TestJobs.Succeeding.Kind, typeof(Jobs.TestJobs.Echo)),
        }.Select(x => new JobHandlerRegistration(x.Kind, typeof(object), x.Result))).Configure(options);
        return options;
    }
}
