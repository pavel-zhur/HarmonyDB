// The "uri" cases of generate.ts: System.Uri over URIs put together from nasty parts (schemes, user info, hosts of
// every kind and script, ports, paths, queries and fragments with every kind of escape), whole odd strings, families
// of variants of one URI (for ==), resolution against a base, JSON values, and the string hash.
// A string with a lone surrogate travels as the array of its code units (see uriSuite.ts).

import type { Case, Random } from './generate';

const schemes = ['http', 'https', 'HTTP', 'HtTpS', 'ftp', 'file', 'FILE', 'mailto', 'news', 'uuid', 'ws', 'wss', 'ldap', 'net.tcp', 'net.pipe',
    'gopher', 'telnet', 'nntp', 'foo', 'a+b-c.d', 'vsmacros', 'x', '1abc', '', 'httpſ', 'ſhttp', 'ht tp', 'Ж', 'http:', 'https '];

const separators = ['://', '://', '://', ':', ':/', ':///', ':\\\\', ':/\\', '//', ':////'];

const userInfos = ['', '', '', '', 'user@', 'user:pass@', 'u%40x@', 'ю@', '@', 'a b@', 'U%3Ap@', 'u@v@', 'us%zzer@', 'user:@'];

const hosts = [
    'example.com', 'EXAMPLE.COM', 'Example.Com', 'www.example.org', 'tabs.example.net', 'localhost', 'LOCALHOST', 'loopback', 'LoopBack',
    'пример.рф', 'ПРИМЕР.РФ', 'Пример.Com', 'xn--e1afmkfd.xn--p1ai', 'XN--E1AFMKFD.xn--p1ai', 'bücher.de', 'BÜCHER.de', '例子.测试', 'مثال.إختبار',
    'a\u200eb.com', 'e\u0301x.com', 'ex%41mple.com', 'ex%D0%96.com', 'é.com', 'a_b.com', '-a.com', 'a-.com', 'a..b', 'a.b.', '.', '..', '',
    'a b', 'a%20b', 'host:', '192.168.0.1', '0x7f.1', '127.1', '010.0.0.1', '4294967295', '4294967296', '256.1.1.1', '1.2.3', '1.2.3.4.5',
    '0x100000000', '08.1.1.1', '[::1]', '[::ffff:192.168.0.1]', '[2001:DB8::1]', '[fe80::1%eth0]', '[::1/64]', '[1:2:3:4:5:6:7:8]', '[::]',
    '[::1', '[g::1]', '[1::2::3]', '[0:0:0:0:0:0:0:1]', '[::5efe:1.2.3.4]', 'a'.repeat(63) + '.com', 'a'.repeat(64) + '.com',
    'x'.repeat(70), 'ä'.repeat(40) + '.de', 'a．b。c', 'a~b', 'a!b', 'a$b', 'a\\b', 'ab\u00a0c', '\u00a0x', 'ı.com', 'K.com',
];

const ports = ['', '', '', '', ':', ':0', ':80', ':443', ':21', ':8080', ':65535', ':65536', ':99999', ':08', ':-1', ':a', ':80a', ':0080'];

const paths = [
    '', '', '/', '/', '/a/b', '/a/b/', '/a/./b/../c', '/../a', '/a/../../b', '/a//b', '\\a\\b', '/a\\b', '/a%20b', '/%2F', '/%2f', '/%2e%2e/x',
    '/%2E/', '/%5C', '/Ж', '/%D0%96', '/%d0%96', '/%D0', '/%D0%41', '/%zz', '/%', '/%E2%82', '/%E2%82%AC', '/%F0%9F%98%80', '/%F0%9F%98',
    '/%ED%A0%80', '/%C0%AF', '/%FF', '/😀', '/a b', '/a"b<c>', '/{x}|^`', '/%7E', '/%41', '/%00', '/%7F', '/%C2%A0', '/%C2%80', '/\u00a0',
    '/\u0001', '/\u0085', '/\ue000', '/\ufffe', '/;x', '/a:b', '/~user', '/.', '/..', '/a/.', '/a/..', '/...', '/a/.../b', '/C:/x', '/c|/x',
    '/chords/artist/84542/song_title/', '/tab/artist/song-title-chords-1234', '/песни/Кино', '/%D0%9A%D0%B8%D0%BD%D0%BE', '/a%2Fb%3Fc%23d',
    '/%25', '/%25%32%30', '/a+b', '/a&b=c', '/%3B%40%26%3D%2B%24%2C', '/%21%27%28%29%2A', '/\u200e', '/%E2%80%8E', '/%EF%BF%BD', '/\uDB40\uDC01',
];

const queries = ['', '', '', '?', '?a=b', '?a=%20&b=%D0%96', '?q=Ж', '?q=a b', '?q=%23', '?x=%zz', '?\ue000', '?%EE%80%80', '?a=b?c', '?a/b\\c',
    '?q=%2F%3F', '?q=%C3%A9', '?q=é', '?x=%25', '?😀', '?%F3%B0%80%80', '?\uDB80\uDC00', '?a=\u0001'];

const fragments = ['', '', '', '#', '#f', '#Ж', '#%20', '#a#b', '#%D0%96', '#a b', '#a?b', '#%23', '#\ue000', '#%zz'];

const wholeStrings = [
    '', ' ', '  http://a.com  ', '\thttp://a.com\n', 'http:a.com', 'http:/a.com', 'http:///a.com', 'http://', 'https://', 'http://a.com\\b',
    '//server/share', '//server', '\\\\server\\share\\x', '\\\\server', 'C:\\x\\y', 'C:/x/../y', 'c|/x', 'C:', 'C:x', '/usr/x', '/', '//',
    'rel/path', '../x', './x', '?q', '#f', 'x:', 'a:b', 'ab:c', '1a:b', 'mailto:a@b.com', 'mailto:A@B.COM?subject=x', 'mailto:', 'mailto:@b.com',
    'news:comp.lang', 'news:a b', 'uuid:123', 'file:///C:/x', 'file:///c|/x', 'file://server/share', 'file:///home/x', 'file:/x', 'file:x',
    'file://localhost/x', 'file:////server/x', 'javascript:alert(1)', 'data:text/plain,x', 'urn:isbn:123', 'http://a.com:80', 'https://a.com:443/',
    'HTTPS://EXAMPLE.COM/A?B#C', 'http://[::1]', 'http://user@[::1]:8080/x', 'https://www.example.org/chords/artist/84542/song_title/',
    'https://tabs.example.net/tab/artist/song-title-chords-1234', 'https://example.net/ru/artist/1234-song-title.html',
    'https://пример.рф/песня', 'https://example.org/chords/%D0%9A%D0%B8%D0%BD%D0%BE', 'http://a.com/%', 'http://a.com/%2', 'foo://./x',
    'foo://../x', 'foo://.../x', 'foo:/x', 'foo:x', 'foo:', 'foo://', 'foo://a\\b', 'foo://a:b/c', 'ws://a.com:80/', 'net.tcp://a/b',
    'http://a.com/a/b/c/../../../../d', 'http://a.com/./', 'http://a.com/..', 'http://a.com/%2e%2E', 'file:///a/%2e%2e/b', 'file://a/%2F',
    'http://a.com?q', 'http://a.com#f', 'http://a.com?', 'http://a.com#', 'http://%41.com', 'http://a.com:/x', 'http://a .com/', 'http://a.com /x',
    'http://a.com/x ', 'http://a.com/\u3000', 'http://\u0430.com', 'https://ex\u00ADample.com', 'http://a%2Fb.com', 'http://-/', 'http://_/',
    'http://a/b', 'HTTP://A/B', 'http://a/B', 'http://1.2.3.4:5/', 'http://[::1]:080', 'http://u:p@h:1/p?q#f',
];

const relatives = [null, '', ' ', 'x', '/x', '//other/x', '\\x', '\\\\x', '?q', '#f', '../x', './x', '..', 'a/../b', 'C:\\x', 'c:/x', 'http:x',
    'https:x', 'http://other/x', 'foo:bar', 'mailto:x', 'Ж', '%D0%96', 'a b', 'a?b#c', ':x', 'x:y', 'V3/GetCollection', 'V3/GetCollection?code={code}'];

const bases = ['http://localhost:7071/api/', 'https://a.com/b/c', 'https://a.com', 'http://u@[::1]:8080/x/y', 'file:///C:/x/y', 'file://server/share/x',
    '\\\\server\\share\\x', 'C:\\x\\y', '/usr/x', 'mailto:a@b.com', 'foo:x/y', 'https://пример.рф/путь/', 'http://a.com/x?q#f', 'news:a'];

const jsonRaw = ['"https://a.com/x"', '"rel"', '""', '"http://a b"', 'null', '1', 'true', '{}', '[]', '"\\u0416"', '"https://a.com/\\ud800"',
    '"\\t http://a.com \\n"', '"http://[::1"'];

/** A string with a lone surrogate as the array of its code units. */
function encode(s: string): string | number[] {
    const valid = !/[\ud800-\udbff](?![\udc00-\udfff])|(?<![\ud800-\udbff])[\udc00-\udfff]/.test(s);
    return valid ? s : Array.from({ length: s.length }, (_, i) => s.charCodeAt(i));
}

function composed(random: Random): string {
    const scheme = random.chance(0.85) ? random.pick(['http', 'https', 'https', 'http', 'HTTPS']) : random.pick(schemes);
    const separator = random.chance(0.85) ? '://' : random.pick(separators);
    return scheme + separator + random.pick(userInfos) + random.pick(hosts) + random.pick(ports) + random.pick(paths) + random.pick(queries) + random.pick(fragments);
}

/** Variants of one URI that .NET may or may not call equal. */
function family(random: Random): string[] {
    const host = random.pick(hosts.slice(0, 20));
    const path = random.pick(paths);
    const query = random.pick(queries);
    const base = `https://${host}${path}${query}`;
    const variants = [
        base,
        `HTTPS://${host.toUpperCase()}${path}${query}`,
        `https://${host}:443${path}${query}`,
        `https://${host}${path}${query}#frag`,
        `https://user@${host}${path}${query}`,
        `https://${host}${path.toUpperCase()}${query}`,
        `https://${host}${path.replace(/[a-z]/, c => '%' + c.charCodeAt(0).toString(16).toUpperCase())}${query}`,
        `https://${host}${encodeURI(path)}${query}`,
        `http://${host}${path}${query}`,
        `https://${host}.${path}${query}`,
        `https://${host}${path}/.${query}`,
        `https://${host}${path}${query === '' ? '?' : query}`,
    ];
    return Array.from({ length: random.int(3, 7) }, () => random.pick(variants));
}

export function generateUriCases(random: Random, scale: number): Case[] {
    const cases: Case[] = [];
    let n = 0;
    const add = (input: unknown) => cases.push({ id: `uri-${n++}`, suite: 'uri', input });

    for (let i = 0; i < wholeStrings.length; i += 6) add({ f: 'uri', u: wholeStrings.slice(i, i + 6).map(encode) });
    for (const host of hosts) add({ f: 'uri', u: [`http://${host}/`, `https://${host}:8080/p?q#f`, `foo://${host}/x`, `file://${host}/x`].map(encode) });
    for (const path of paths) add({ f: 'uri', u: [`http://a.com${path}`, `file:///c:${path}`, `foo://a${path}`, `news:${path}`, path].map(encode) });
    for (let i = 0; i < 400 * scale; i++) add({ f: 'uri', u: Array.from({ length: random.int(1, 5) }, () => encode(composed(random))) });
    for (let i = 0; i < 100 * scale; i++) add({ f: 'uri', u: family(random).map(encode) });
    for (const b of bases) add({ f: 'combine', b: encode(b), r: relatives });
    for (let i = 0; i < 40 * scale; i++) add({ f: 'combine', b: encode(composed(random)), r: Array.from({ length: random.int(1, 6) }, () => random.chance(0.5) ? random.pick(relatives) : random.pick(paths) + random.pick(queries)) });
    add({ f: 'json', raw: jsonRaw });

    const marvinStrings = ['', 'a', 'Aa', 'BB', 'ab', 'abc', 'abcd', 'abcde', 'abcdefg', 'abcdefgh', 'abcdefghi', 'Кино', 'КИНО', 'straße', 'İı', 'ſ',
        '😀', '\ud800', '\udc00x', 'x'.repeat(100), 'https://www.example.org/chords/artist/84542/song_title/', 'ǅ', 'ﬀ', 'µ', 'ÿ'];
    add({ f: 'marvin', s: marvinStrings.map(encode) });
    for (let i = 0; i < 10 * scale; i++) {
        add({ f: 'marvin', s: Array.from({ length: 8 }, () => encode(Array.from({ length: random.int(0, 20) }, () => String.fromCharCode(random.chance(0.7) ? random.int(32, 127) : random.int(0, 0x10000))).join(''))) });
    }

    return cases;
}
