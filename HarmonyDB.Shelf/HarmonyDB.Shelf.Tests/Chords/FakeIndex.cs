using System.Net;
using System.Text;
using System.Text.Json;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Tests.Api;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace HarmonyDB.Shelf.Tests.Chords;

/// <summary>
/// The index, as the shelf calls it: every request is kept (its operation and body), and answered by
/// <see cref="Answer"/>.
/// </summary>
public sealed class FakeIndex : HttpMessageHandler
{
    public const string Endpoint = "http://index.test/index/api/";
    public const string Key = "the-shelf-key-at-the-index-0123456789";

    public List<(string Operation, JsonElement Body)> Requests { get; } = [];

    /// <summary>
    /// The operation and the request's body → the status and the answer (written with the source contract's JSON).
    /// </summary>
    public Func<string, JsonElement, (HttpStatusCode Status, object? Body)> Answer { get; set; } = (_, _) => (HttpStatusCode.InternalServerError, null);

    /// <summary>
    /// The HTTP request and the file's request (<see cref="GetFileRequest"/>, as JSON) → the file's answer.
    /// </summary>
    public Func<HttpRequestMessage, JsonElement, HttpResponseMessage> FileAnswer { get; set; } = (_, _) => new(HttpStatusCode.InternalServerError);

    public IEnumerable<JsonElement> Callers(string operation)
        => Requests.Where(x => x.Operation == operation).Select(x => x.Body.GetProperty("caller"));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal(Key, request.Headers.GetValues("X-Internal-Key").Single());
        var operation = request.RequestUri!.AbsolutePath.Split('/').Last();
        if (operation == SourceV2Urls.V2GetFile)
        {
            // the request travels in a header; the answer is the file's
            var file = GetFileRequest.Decode(request.Headers.GetValues(GetFileRequest.HeaderName).Single());
            Assert.NotNull(file);
            var fileBody = JsonSerializer.SerializeToElement(file, SourceV2Json.Options);
            lock (Requests)
                Requests.Add((operation, fileBody));

            return FileAnswer(request, fileBody);
        }

        var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
        lock (Requests)
            Requests.Add((operation, body));

        var (status, answer) = Answer(operation, body);
        return new(status)
        {
            Content = answer == null ? null : new StringContent(JsonSerializer.Serialize(answer, SourceV2Json.Options), Encoding.UTF8, "application/json"),
        };
    }

    /// <summary>
    /// The test API with this index behind it, and the settings given.
    /// </summary>
    public WebApplicationFactory<Program> Api(ShelfApiFactory factory, IReadOnlyDictionary<string, string?>? settings = null)
    {
        factory.Settings["Clients:Index:Endpoint"] = Endpoint;
        factory.Settings["Clients:Index:Key"] = Key;
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            factory.Settings[key] = value;

        return factory.WithWebHostBuilder(x => x.ConfigureTestServices(services =>
            services.AddHttpClient<IndexClient>().ConfigurePrimaryHttpMessageHandler(() => this)));
    }

    public static Header Header(string id, string label, string? icon = null) => new()
    {
        Id = id,
        Source = new() { Key = label, Title = label, Icon = icon },
        Title = $"Title of {id}",
        Artists = ["An artist"],
        Kinds = [ContentKind.Sheet],
        Attributes = new() { Key = "Am" },
        IsPublic = true,
    };

    /// <summary>
    /// A request as a person of the test sign-in, with the permissions given, or a guest.
    /// </summary>
    public static HttpRequestMessage As(HttpMethod method, string path, Guid? account, string permissions = "", object? body = null, string? clientAllowed = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (account != null)
        {
            request.Headers.Add(ShelfApiFactory.AccountHeader, account.ToString());
            request.Headers.Add(ShelfApiFactory.PermissionsHeader, permissions);
            if (clientAllowed != null)
                request.Headers.Add(ShelfApiFactory.ClientAllowedHeader, clientAllowed);
        }

        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json");
        return request;
    }
}
