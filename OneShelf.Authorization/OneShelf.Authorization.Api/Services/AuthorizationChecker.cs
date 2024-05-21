using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Authorization.Api.Model;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Services;

namespace OneShelf.Authorization.Api.Services;

public class AuthorizationChecker
{
    private readonly ILogger<AuthorizationChecker> _logger;
    private readonly SongsOperations _songsOperations;
    private readonly AuthorizationOptions _authorizationOptions;

    public AuthorizationChecker(ILogger<AuthorizationChecker> logger, SongsOperations songsOperations, IOptions<AuthorizationOptions> authorizationOptions)
    {
        _logger = logger;
        _songsOperations = songsOperations;
        _authorizationOptions = authorizationOptions.Value;
    }

    public async Task<(string? authorizationError, int? tenantId, bool? arePdfsAllowed)> Check(Identity identity)
    {
        if (!(_authorizationOptions.AllowBadHash && identity.Hash == _authorizationOptions.BadHash || CheckHash(identity)))
        {
            _logger.LogInformation("Bad hash: {identity}.", identity);
            return ("BadHash", null, null);
        }

        var user = await _songsOperations.SongsDatabase.Users.Include(x => x.Tenant).SingleOrDefaultAsync(x => x.Id == identity.Id);

        if (user == null)
        {
            user = new()
            {
                Id = identity.Id,
                Tenant = new()
                {
                    PrivateDescription = Tenant.PersonalPrivateDescription(identity.Id),
                },
                CreatedOn = DateTime.Now,
                Title = identity.Title,
            };

            _songsOperations.SongsDatabase.Users.Add(user);
            await _songsOperations.SongsDatabase.SaveChangesAsyncX();

            await _songsOperations.InitializeTenant(user.TenantId);
        }

        return (null, user.TenantId, user.Tenant.ArePdfsAllowed);
    }

    private bool CheckHash(Identity identity)
    {
        var dataCheckString = string.Join("\n",
            JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(identity))
                .Where(x => x.Value != null && x.Key != "hash")
                .OrderBy(x => x.Key)
                .Select(x => $"{x.Key}={x.Value}"));

        return _authorizationOptions.BotTokens.Any(botToken =>
        {
            var botTokenHash = Sha256Hash(botToken);

            var hash = HMACSHA256.HashData(botTokenHash, Encoding.UTF8.GetBytes(dataCheckString));

            var hashString = string.Join(string.Empty, hash.Select(i => i.ToString("x2")));

            return hashString == identity.Hash;
        });
    }

    private static byte[] Sha256Hash(string value)
    {
        using (SHA256 hash = SHA256.Create())
        {
            return hash.ComputeHash(Encoding.UTF8.GetBytes(value));
        }
    }

    private static byte[] HashHmac(byte[] key, byte[] message)
    {
        var hash = new HMACSHA256(key);
        return hash.ComputeHash(message);
    }
}