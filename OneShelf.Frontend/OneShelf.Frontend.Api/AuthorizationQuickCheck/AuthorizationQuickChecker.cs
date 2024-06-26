using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Frontend.Api.Model.V3.Api;

namespace OneShelf.Frontend.Api.AuthorizationQuickCheck;

public class AuthorizationQuickChecker
{
    private readonly SecurityContext _securityContext;
    private readonly AuthorizationQuickCheckOptions _options;

    public AuthorizationQuickChecker(IOptions<AuthorizationQuickCheckOptions> options, SecurityContext securityContext)
    {
        _securityContext = securityContext;
        _options = options.Value;
    }

    public async Task<string> CreateV1PreviewPdfLink(GetPdfsChunkRequestFile requestFile,
        string urlAbsolutePathBase)
    {
        var expiration = DateTime.Now.AddMinutes(30).Ticks;

        var request = new PreviewPdfRequest
        {
            File = requestFile,
            Expiration = expiration,
            UserId = _securityContext.Identity.Id,
            Hash = await Sign(JsonSerializer.Serialize(requestFile), expiration),
        };

        return $"{urlAbsolutePathBase}{ApiUrls.V3PreviewPdf}?x={Uri.EscapeDataString(JsonSerializer.Serialize(request))}";
    }

    public async Task<string> Sign(string file, long expiration) =>
        await Sign($"{_securityContext.Identity.Id}, {expiration}, {file}, {_options.Secret}");

    private async Task<string> Sign(string data)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(await SHA1.HashDataAsync(stream));
    }
}