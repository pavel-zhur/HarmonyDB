// What .NET 9 gives (checked in Docker, as tools/dotnet-reference does over a broad generated set).

import { describe, expect, it } from 'vitest';
import { InvalidOperationException } from './exceptions';
import { deserialize, jsonUri, serialize } from './json/JsonSerializer';
import { Uri, UriFormatException, UriKind } from './Uri';

describe('System.Uri', () => {
    it('ToString decodes what is safe, lower-cases an ASCII host, drops a default port, compresses the path', () => {
        expect(new Uri('HTTP://Example.COM').toString()).toBe('http://example.com/');
        expect(new Uri('https://example.com/a%20b/%D0%96/%41%2F%3F?x=%20%D0%96').toString()).toBe('https://example.com/a b/Ж/A%2F%3F?x= Ж');
        expect(new Uri('https://a.com:443/x/../y/./z\\w').toString()).toBe('https://a.com/y/z/w');
        expect(new Uri('https://a.com/%zz%').toString()).toBe('https://a.com/%25zz%25');
    });

    it('Host keeps a Unicode host as written (upper case too), lower-cases ASCII', () => {
        expect(new Uri('https://ПРИМЕР.рф/Путь').host).toBe('ПРИМЕР.рф');
        expect(new Uri('https://xn--e1afmkfd.xn--p1ai/').host).toBe('xn--e1afmkfd.xn--p1ai');
        expect(new Uri('https://user:pw@Host.com/p').host).toBe('host.com');
        expect(new Uri('http://[::1]:80/').host).toBe('[::1]');
        expect(() => new Uri('rel', UriKind.RelativeOrAbsolute).host).toThrow(InvalidOperationException);
    });

    it('== ignores the fragment, the user info, the case of the scheme and host, a default port and escapes of unreserved characters', () => {
        expect(Uri.equals(new Uri('https://a.com/x#f'), new Uri('HTTPS://user@A.com:443/x#g'))).toBe(true);
        expect(Uri.equals(new Uri('https://a.com/%41'), new Uri('https://a.com/A'))).toBe(true);
        expect(Uri.equals(new Uri('https://a.com/x'), new Uri('https://a.com/X'))).toBe(false);
        expect(Uri.equals(new Uri('https://a.com/x'), new Uri('http://a.com/x'))).toBe(false);
        expect(new Uri('https://a.com/x#f').getHashCode()).toBe(new Uri('https://user@A.com:443/x').getHashCode());
    });

    it('TryCreate (RelativeOrAbsolute) takes relative strings and rejects bad absolute ones', () => {
        expect(Uri.tryCreate('/rel', UriKind.RelativeOrAbsolute)!.isAbsoluteUri).toBe(false);
        expect(Uri.tryCreate('http://a b/', UriKind.RelativeOrAbsolute)).toBeNull();
        expect(Uri.tryCreate('C:\\x\\y', UriKind.RelativeOrAbsolute)!.toString()).toBe('file:///C:/x/y');
        expect(() => new Uri('rel')).toThrow(UriFormatException);
    });

    it('JSON: read with TryCreate, written as the original string', () => {
        const uri = deserialize('"HTTPS://A.com/%41#f"', jsonUri);
        expect(uri.toString()).toBe('https://a.com/A#f');
        expect(serialize(uri, jsonUri)).toBe('"HTTPS://A.com/%41#f"');
        expect(() => deserialize('"http://a b"', jsonUri)).toThrow('could not be converted to System.Uri');
    });

    it('new Uri(Uri, string) resolves as .NET does', () => {
        expect(new Uri(new Uri('https://host/x/api'), 'V3Search').absoluteUri).toBe('https://host/x/V3Search');
        expect(new Uri(new Uri('http://localhost:7071/api/'), 'V3/x?code={code}').toString()).toBe('http://localhost:7071/api/V3/x?code={code}');
    });

    it('EscapeDataString encodes all but the unreserved characters, a lone surrogate as U+FFFD', () => {
        expect(Uri.escapeDataString('Кино ~-_.!\'()*')).toBe('%D0%9A%D0%B8%D0%BD%D0%BE%20~-_.%21%27%28%29%2A');
        expect(Uri.escapeDataString('a\ud800b')).toBe('a%EF%BF%BDb');
    });
});
