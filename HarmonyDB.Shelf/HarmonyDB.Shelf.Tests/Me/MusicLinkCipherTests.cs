using System.Security.Cryptography;
using HarmonyDB.Shelf.Api.Areas.Me.Services;
using HarmonyDB.Shelf.Api.Foundation.Secrets;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Tests.Auth;

namespace HarmonyDB.Shelf.Tests.Me;

public sealed class MusicLinkCipherTests
{
    private const string Credentials = "Cookie: a=b; c=d\nAuthorization: SAPISIDHASH 123_abc";

    private static readonly string KeyOne = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string KeyTwo = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void WhatIsEncryptedOpensOnlyInItsRow()
    {
        var cipher = Cipher(new SecretsOptions { Key = KeyOne, KeyVersion = 1 });
        var link = NewLink();
        cipher.Encrypt(link, Credentials);

        Assert.DoesNotContain(Credentials, System.Text.Encoding.UTF8.GetString(link.Ciphertext));
        Assert.Equal(Credentials, cipher.Decrypt(link));

        // Another row's id, or a changed byte, does not open.
        var moved = NewLink();
        moved.Ciphertext = link.Ciphertext;
        moved.Nonce = link.Nonce;
        moved.Tag = link.Tag;
        moved.KeyVersion = link.KeyVersion;
        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(moved));

        link.Ciphertext[0] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(link));
    }

    [Fact]
    public void AnOldKeyOpensAndTheCurrentOneEncryptsAnew()
    {
        var link = NewLink();
        Cipher(new SecretsOptions { Key = KeyOne, KeyVersion = 1 }).Encrypt(link, Credentials);

        var rotated = Cipher(new SecretsOptions { Key = KeyTwo, KeyVersion = 2, OldKeys = { [1] = KeyOne } });
        Assert.True(rotated.IsUnderOldKey(link));
        Assert.Equal(Credentials, rotated.Decrypt(link));

        rotated.Encrypt(link, rotated.Decrypt(link));
        Assert.Equal(2, link.KeyVersion);
        Assert.False(rotated.IsUnderOldKey(link));

        // With the old key gone, only the re-encrypted row opens.
        Assert.Equal(Credentials, Cipher(new SecretsOptions { Key = KeyTwo, KeyVersion = 2 }).Decrypt(link));
    }

    private static MusicLinkCipher Cipher(SecretsOptions options) => new(OidcProviderTests.Options(options));

    private static MusicLink NewLink() => new()
    {
        Id = Guid.NewGuid(),
        AccountId = Guid.NewGuid(),
        Service = MusicService.Youtube,
        Ciphertext = [],
        Nonce = [],
        Tag = [],
    };
}
