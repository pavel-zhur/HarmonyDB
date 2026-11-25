using System.Security.Cryptography;
using System.Text;
using HarmonyDB.Shelf.Api.Foundation.Secrets;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Me.Services;

/// <summary>
/// What a music link keeps, encrypted at rest: AES-256-GCM under the secrets key of the configuration, with a random
/// 96-bit nonce per encryption and a 128-bit tag. The link's id and service are the associated data, so a ciphertext
/// moved to another row does not open (the account is not: a merge of accounts moves a link whole). The row says its key's version: an old key (still in
/// <c>Shelf:Secrets:OldKeys</c>) opens it, and it is then encrypted anew under the current key.
/// </summary>
public sealed class MusicLinkCipher(IOptionsMonitor<SecretsOptions> secrets)
{
    public void Encrypt(MusicLink link, string credentials)
    {
        var options = secrets.CurrentValue;
        var plaintext = Encoding.UTF8.GetBytes(credentials);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using var aes = new AesGcm(options.GetKey(options.KeyVersion), tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData(link));

        link.Ciphertext = ciphertext;
        link.Nonce = nonce;
        link.Tag = tag;
        link.KeyVersion = options.KeyVersion;
    }

    public string Decrypt(MusicLink link)
    {
        var plaintext = new byte[link.Ciphertext.Length];
        using var aes = new AesGcm(secrets.CurrentValue.GetKey(link.KeyVersion), link.Tag.Length);
        aes.Decrypt(link.Nonce, link.Ciphertext, link.Tag, plaintext, AssociatedData(link));
        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>
    /// Whether the link is encrypted under an older key than the current one.
    /// </summary>
    public bool IsUnderOldKey(MusicLink link) => link.KeyVersion != secrets.CurrentValue.KeyVersion;

    private static byte[] AssociatedData(MusicLink link) => Encoding.UTF8.GetBytes($"music_link:{link.Id}:{link.Service}");
}
