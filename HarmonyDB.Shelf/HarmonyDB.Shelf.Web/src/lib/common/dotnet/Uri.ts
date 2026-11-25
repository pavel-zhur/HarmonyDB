// System.Uri, as much of it as the site and the libraries it runs use: the construction (new Uri(string),
// new Uri(string, UriKind), Uri.TryCreate, new Uri(Uri, string)), OriginalString, IsAbsoluteUri, ToString(), Host,
// AbsoluteUri, GetComponents, Equals and ==, GetHashCode, Uri.EscapeDataString.
//
// Ported statement by statement from .NET 9's System.Private.Uri (Uri.cs, UriExt.cs, UriHelper.cs, IriHelper.cs,
// PercentEncodingHelper.cs, DomainNameHelper.cs, IPv4AddressHelper.cs, IPv6AddressHelper.cs, UncNameHelper.cs,
// UriSyntax.cs), as it runs outside Windows (the browser is not Windows), keeping the lazy parsing and the flags, so
// that every odd result stays the same. The site runs with invariant globalization: string.Normalize is then the
// identity, and IdnMapping is not reached by these members. Only the built-in schemes' parsers exist (the site
// registers none). Checked against .NET by tools/dotnet-reference ("uri" suite).
//
// The overloads: new Uri(string, UriKind = Absolute) and new Uri(Uri, string | null), told apart by the first argument.

import { ArgumentOutOfRangeException, FormatException, IndexOutOfRangeException, InvalidOperationException } from './exceptions';
import { stringHashCode, stringHashCodeOrdinalIgnoreCase } from './hashCode';
import { toLowerInvariant, toUpperInvariant } from './text';

/** System.UriFormatException (a FormatException). */
export class UriFormatException extends FormatException {
    constructor(message: string) {
        super(message);
        this.name = 'UriFormatException';
    }
}

export enum UriKind {
    RelativeOrAbsolute = 0,
    Absolute = 1,
    Relative = 2,
}

export enum UriComponents {
    Scheme = 1,
    UserInfo = 2,
    Host = 4,
    Port = 8,
    Path = 0x10,
    Query = 0x20,
    Fragment = 0x40,
    StrongPort = 0x80,
    NormalizedHost = 0x100,
    KeepDelimiter = 0x40000000,
    SerializationInfoString = -0x80000000,
    AbsoluteUri = 0x7f,
    HostAndPort = 0x84,
    StrongAuthority = 0x86,
    SchemeAndServer = 0xd,
    HttpRequestUrl = 0x3d,
    PathAndQuery = 0x30,
}

export enum UriFormat {
    UriEscaped = 1,
    Unescaped = 2,
    SafeUnescaped = 3,
}

/** The internal format of ToString(). */
const V1ToString = 0x7fff;

enum ParsingError {
    None = 0,
    BadFormat = 1,
    BadScheme = 2,
    BadAuthority = 3,
    EmptyUriString = 4,
    SchemeLimit = 5,
    SizeLimit = 6,
    MustRootedPath = 7,
    BadHostName = 8,
    NonEmptyHost = 9,
    BadPort = 10,
    BadAuthorityTerminator = 11,
    CannotCreateRelative = 12,
}

const messages = {
    notAbsolute: 'This operation is not supported for a relative URI.',
    badFormat: 'Invalid URI: The format of the URI could not be determined.',
    badScheme: 'Invalid URI: The URI scheme is not valid.',
    badAuthority: 'Invalid URI: The Authority/Host could not be parsed.',
    emptyUri: 'Invalid URI: The URI is empty.',
    schemeLimit: 'Invalid URI: The Uri scheme is too long.',
    sizeLimit: 'Invalid URI: The Uri string is too long.',
    mustRootedPath: 'Invalid URI: A Dos path must be rooted, for example, \'c:\\\'.',
    badHostName: 'Invalid URI: The hostname could not be parsed.',
    badPort: 'Invalid URI: Invalid port specified.',
    badAuthorityTerminator: 'Invalid URI: The Authority/Host cannot end with a backslash character (\'\\\').',
    cannotCreateRelative: 'A relative URI cannot be created because the \'uriString\' parameter represents an absolute URI.',
    notJustSerialization: 'UriComponents.SerializationInfoString must not be combined with other UriComponents.',
};

function getException(err: ParsingError): UriFormatException | null {
    switch (err) {
        case ParsingError.None: return null;
        case ParsingError.BadFormat: return new UriFormatException(messages.badFormat);
        case ParsingError.BadScheme: return new UriFormatException(messages.badScheme);
        case ParsingError.BadAuthority: return new UriFormatException(messages.badAuthority);
        case ParsingError.EmptyUriString: return new UriFormatException(messages.emptyUri);
        case ParsingError.SchemeLimit: return new UriFormatException(messages.schemeLimit);
        case ParsingError.SizeLimit: return new UriFormatException(messages.sizeLimit);
        case ParsingError.MustRootedPath: return new UriFormatException(messages.mustRootedPath);
        case ParsingError.BadHostName: return new UriFormatException(messages.badHostName);
        case ParsingError.NonEmptyHost: return new UriFormatException(messages.badFormat);
        case ParsingError.BadPort: return new UriFormatException(messages.badPort);
        case ParsingError.BadAuthorityTerminator: return new UriFormatException(messages.badAuthorityTerminator);
        case ParsingError.CannotCreateRelative: return new UriFormatException(messages.cannotCreateRelative);
        default: return new UriFormatException(messages.badFormat);
    }
}

// Uri.Flags (a ulong there).
const F = {
    Zero: 0n,
    SchemeNotCanonical: 0x1n,
    UserNotCanonical: 0x2n,
    HostNotCanonical: 0x4n,
    PortNotCanonical: 0x8n,
    PathNotCanonical: 0x10n,
    QueryNotCanonical: 0x20n,
    FragmentNotCanonical: 0x40n,
    E_UserNotCanonical: 0x80n,
    E_HostNotCanonical: 0x100n,
    E_PortNotCanonical: 0x200n,
    E_PathNotCanonical: 0x400n,
    E_QueryNotCanonical: 0x800n,
    E_FragmentNotCanonical: 0x1000n,
    ShouldBeCompressed: 0x2000n,
    FirstSlashAbsent: 0x4000n,
    BackslashInPath: 0x8000n,
    IndexMask: 0xffffn,
    HostTypeMask: 0x70000n,
    IPv6HostType: 0x10000n,
    IPv4HostType: 0x20000n,
    DnsHostType: 0x30000n,
    UncHostType: 0x40000n,
    BasicHostType: 0x50000n,
    UserEscaped: 0x80000n,
    AuthorityFound: 0x100000n,
    HasUserInfo: 0x200000n,
    LoopbackHost: 0x400000n,
    NotDefaultPort: 0x800000n,
    UserDrivenParsing: 0x1000000n,
    CanonicalDnsHost: 0x2000000n,
    ErrorOrParsingRecursion: 0x4000000n,
    DosPath: 0x8000000n,
    UncPath: 0x10000000n,
    ImplicitFile: 0x20000000n,
    MinimalUriInfoSet: 0x40000000n,
    AllUriInfoSet: 0x80000000n,
    IdnHost: 0x100000000n,
    HasUnicode: 0x200000000n,
    UserIriCanonical: 0x8000000000n,
    PathIriCanonical: 0x10000000000n,
    QueryIriCanonical: 0x20000000000n,
    FragmentIriCanonical: 0x40000000000n,
    UnixPath: 0x100000000000n,
    DisablePathAndQueryCanonicalization: 0x200000000000n,
} as const;

/** All but the index (the low 16 bits). */
const notIndexMask = 0xffffffffffff0000n;

// UriSyntaxFlags.
const S = {
    MustHaveAuthority: 0x1,
    OptionalAuthority: 0x2,
    MayHaveUserInfo: 0x4,
    MayHavePort: 0x8,
    MayHavePath: 0x10,
    MayHaveQuery: 0x20,
    MayHaveFragment: 0x40,
    AllowEmptyHost: 0x80,
    AllowUncHost: 0x100,
    AllowDnsHost: 0x200,
    AllowIPv4Host: 0x400,
    AllowIPv6Host: 0x800,
    AllowAnInternetHost: 0xe00,
    AllowAnyOtherHost: 0x1000,
    FileLikeUri: 0x2000,
    MailToLikeUri: 0x4000,
    V1_UnknownUri: 0x10000,
    SimpleUserSyntax: 0x20000,
    AllowDOSPath: 0x100000,
    PathIsRooted: 0x200000,
    ConvertPathSlashes: 0x400000,
    CompressPath: 0x800000,
    CanonicalizeAsFilePath: 0x1000000,
    UnEscapeDotsAndSlashes: 0x2000000,
    AllowIdn: 0x4000000,
    AllowIriParsing: 0x10000000,
} as const;

// Uri.Check.
const C = {
    None: 0,
    EscapedCanonical: 1,
    DisplayCanonical: 2,
    DotSlashAttn: 4,
    FoundNonAscii: 8,
    BackslashInPath: 0x10,
    ReservedFound: 0x20,
    NotIriCanonical: 0x40,
    DotSlashEscaped: 0x80,
} as const;

// UnescapeMode.
const M = {
    CopyOnly: 0,
    Escape: 1,
    Unescape: 2,
    EscapeUnescape: 3,
    V1ToStringFlag: 4,
    UnescapeAll: 8,
} as const;

/** UriParser: the built-in ones only (a BuiltInUriParser is always "simple"). */
class UriParser {
    constructor(readonly schemeName: string, readonly defaultPort: number, readonly flags: number) {
        this.flags = flags | S.SimpleUserSyntax;
    }

    inFact(flags: number): boolean {
        return (this.flags & flags) !== 0;
    }

    notAny(flags: number): boolean {
        return (this.flags & flags) === 0;
    }

    isAllSet(flags: number): boolean {
        return (this.flags & flags) === flags;
    }

    get isSimple(): boolean {
        return this.inFact(S.SimpleUserSyntax);
    }
}

const httpSyntaxFlags = S.AllowAnInternetHost | S.MustHaveAuthority | S.MayHaveUserInfo | S.MayHavePort | S.MayHavePath | S.MayHaveQuery | S.MayHaveFragment
    | S.AllowUncHost | S.PathIsRooted | S.ConvertPathSlashes | S.CompressPath | S.CanonicalizeAsFilePath | S.AllowIdn | S.AllowIriParsing;
const newsSyntaxFlags = S.MayHavePath | S.MayHaveFragment | S.AllowIriParsing;

const HttpUri = new UriParser('http', 80, httpSyntaxFlags);
const HttpsUri = new UriParser('https', 443, httpSyntaxFlags);
const WsUri = new UriParser('ws', 80, httpSyntaxFlags);
const WssUri = new UriParser('wss', 443, httpSyntaxFlags);
const FtpUri = new UriParser('ftp', 21, S.AllowAnInternetHost | S.MustHaveAuthority | S.MayHaveUserInfo | S.MayHavePort | S.MayHavePath | S.MayHaveFragment
    | S.AllowUncHost | S.PathIsRooted | S.ConvertPathSlashes | S.CompressPath | S.CanonicalizeAsFilePath | S.AllowIdn | S.AllowIriParsing);
const FileUri = new UriParser('file', -1, S.AllowAnInternetHost | S.MustHaveAuthority | S.MayHavePath | S.MayHaveQuery | S.MayHaveFragment | S.AllowEmptyHost
    | S.AllowUncHost | S.FileLikeUri | S.AllowDOSPath | S.PathIsRooted | S.ConvertPathSlashes | S.CompressPath | S.CanonicalizeAsFilePath
    | S.UnEscapeDotsAndSlashes | S.AllowIdn | S.AllowIriParsing);
const UnixFileUri = new UriParser('file', -1, S.AllowAnInternetHost | S.MustHaveAuthority | S.MayHavePath | S.MayHaveQuery | S.MayHaveFragment | S.AllowEmptyHost
    | S.AllowUncHost | S.FileLikeUri | S.AllowDOSPath | S.PathIsRooted | S.CompressPath | S.CanonicalizeAsFilePath | S.UnEscapeDotsAndSlashes
    | S.AllowIdn | S.AllowIriParsing);
const GopherUri = new UriParser('gopher', 70, S.AllowAnInternetHost | S.MustHaveAuthority | S.MayHaveUserInfo | S.MayHavePort | S.MayHavePath
    | S.MayHaveFragment | S.AllowUncHost | S.PathIsRooted | S.AllowIdn | S.AllowIriParsing);
const NntpUri = new UriParser('nntp', 119, S.AllowAnInternetHost | S.MustHaveAuthority | S.MayHaveUserInfo | S.MayHavePort | S.MayHavePath
    | S.MayHaveFragment | S.AllowUncHost | S.PathIsRooted | S.AllowIdn | S.AllowIriParsing);
const NewsUri = new UriParser('news', -1, newsSyntaxFlags);
const MailToUri = new UriParser('mailto', 25, S.AllowAnInternetHost | S.MayHaveUserInfo | S.MayHavePort | S.MayHavePath | S.MayHaveQuery | S.MayHaveFragment
    | S.AllowEmptyHost | S.AllowUncHost | S.MailToLikeUri | S.AllowIdn | S.AllowIriParsing);
const UuidUri = new UriParser('uuid', -1, newsSyntaxFlags);
const TelnetUri = new UriParser('telnet', 23, S.AllowAnInternetHost | S.MustHaveAuthority | S.MayHaveUserInfo | S.MayHavePort | S.MayHavePath
    | S.MayHaveFragment | S.AllowUncHost | S.PathIsRooted | S.AllowIdn | S.AllowIriParsing);
const LdapUri = new UriParser('ldap', 389, S.AllowAnInternetHost | S.MustHaveAuthority | S.MayHaveUserInfo | S.MayHavePort | S.MayHavePath | S.MayHaveQuery
    | S.MayHaveFragment | S.AllowEmptyHost | S.AllowUncHost | S.PathIsRooted | S.AllowIdn | S.AllowIriParsing);
const NetTcpUri = new UriParser('net.tcp', 808, S.AllowAnInternetHost | S.MustHaveAuthority | S.MayHavePort | S.MayHavePath | S.MayHaveQuery | S.MayHaveFragment
    | S.PathIsRooted | S.ConvertPathSlashes | S.CompressPath | S.CanonicalizeAsFilePath | S.UnEscapeDotsAndSlashes | S.AllowIdn | S.AllowIriParsing);
const NetPipeUri = new UriParser('net.pipe', -1, S.AllowAnInternetHost | S.MustHaveAuthority | S.MayHavePath | S.MayHaveQuery | S.MayHaveFragment
    | S.PathIsRooted | S.ConvertPathSlashes | S.CompressPath | S.CanonicalizeAsFilePath | S.UnEscapeDotsAndSlashes | S.AllowIdn | S.AllowIriParsing);
const VsMacrosUri = new UriParser('vsmacros', -1, S.AllowAnInternetHost | S.MustHaveAuthority | S.MayHavePath | S.MayHaveFragment | S.AllowEmptyHost
    | S.AllowUncHost | S.FileLikeUri | S.AllowDOSPath | S.ConvertPathSlashes | S.CompressPath | S.CanonicalizeAsFilePath | S.UnEscapeDotsAndSlashes
    | S.AllowIdn | S.AllowIriParsing);

const syntaxTable = new Map<string, UriParser>([HttpUri, HttpsUri, WsUri, WssUri, FtpUri, FileUri, GopherUri, NntpUri, NewsUri, MailToUri, UuidUri, TelnetUri,
    LdapUri, NetTcpUri, NetPipeUri, VsMacrosUri].map(x => [x.schemeName, x]));
let tempSyntaxTable = new Map<string, UriParser>();

/** UriParser.FindOrFetchAsUnknownV1Syntax: one parser per unknown scheme (the cache starts anew at 512 of them). */
function findOrFetchAsUnknownV1Syntax(lwrCaseScheme: string): UriParser {
    const known = syntaxTable.get(lwrCaseScheme) ?? tempSyntaxTable.get(lwrCaseScheme);
    if (known !== undefined) return known;
    if (tempSyntaxTable.size >= 512) tempSyntaxTable = new Map();
    const parser = new UriParser(lwrCaseScheme, -1, S.AllowAnInternetHost | S.OptionalAuthority | S.MayHaveUserInfo | S.MayHavePort | S.MayHavePath
        | S.MayHaveQuery | S.MayHaveFragment | S.AllowEmptyHost | S.AllowUncHost | S.V1_UnknownUri | S.AllowDOSPath | S.PathIsRooted
        | S.ConvertPathSlashes | S.CompressPath | S.AllowIdn | S.AllowIriParsing);
    tempSyntaxTable.set(lwrCaseScheme, parser);
    return parser;
}

// Characters.

const c_ = (s: string): number => s.charCodeAt(0);
const PERCENT = c_('%'), SLASH = c_('/'), BACKSLASH = c_('\\'), QUESTION = c_('?'), HASH = c_('#'), COLON = c_(':'), PIPE = c_('|'), AT = c_('@'),
    DOT = c_('.'), LBRACKET = c_('['), RBRACKET = c_(']');
const NOCHAR = 0xffff;

/** What a C# pointer reads at an index of a pinned string: the terminating '\0' at its end. */
function pc(s: string, i: number): number {
    return i < s.length ? s.charCodeAt(i) : 0;
}

/** The string indexer: throws past the end. */
function ch(s: string, i: number): number {
    if (i < 0 || i >= s.length) throw new IndexOutOfRangeException();
    return s.charCodeAt(i);
}

const isAsciiLetter = (c: number) => (c >= 0x41 && c <= 0x5a) || (c >= 0x61 && c <= 0x7a);
const isAsciiDigit = (c: number) => c >= 0x30 && c <= 0x39;
const isAsciiLetterOrDigit = (c: number) => isAsciiLetter(c) || isAsciiDigit(c);
const isAsciiHexDigit = (c: number) => isAsciiDigit(c) || (c >= 0x41 && c <= 0x46) || (c >= 0x61 && c <= 0x66);
const isHighSurrogate = (c: number) => c >= 0xd800 && c <= 0xdbff;
const isLowSurrogate = (c: number) => c >= 0xdc00 && c <= 0xdfff;
const letterPattern = /\p{L}/u;
/** char.IsLetter of one UTF-16 code unit. */
const isLetter = (c: number) => letterPattern.test(String.fromCharCode(c));

function asciiTable(chars: string): boolean[] {
    const table = new Array<boolean>(128).fill(false);
    for (const c of chars) table[c.charCodeAt(0)] = true;
    return table;
}

const unreservedChars = '-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz~';
const Unreserved = asciiTable(unreservedChars);
const UnreservedReserved = asciiTable('!#$&\'()*+,-./0123456789:;=?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]_abcdefghijklmnopqrstuvwxyz~');
const UnreservedReservedExceptHash = asciiTable('!$&\'()*+,-./0123456789:;=?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]_abcdefghijklmnopqrstuvwxyz~');
const UnreservedReservedExceptQuestionMarkHash = asciiTable('!$&\'()*+,-./0123456789:;=@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]_abcdefghijklmnopqrstuvwxyz~');
const schemeChars = asciiTable('+-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz');
const domainValidChars = asciiTable('-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz.');
const iriInvalidAscii = asciiTable('!"#$%&\'()*+,/:;<=>?@[\\]^`{|}~');
const notSafeForUnescape = asciiTable(';/?:@&=+$,#[]!\'()*%\\#');

const inTable = (table: boolean[], c: number) => c < 128 && table[c]!;
const isIriInvalid = (c: number) => c <= 0x20 || (c >= 0x7f && c <= 0x9f) || inTable(iriInvalidAscii, c);
const isDotLike = (c: number) => c === DOT || c === 0x3002 || c === 0xff0e || c === 0xff61;

/** UriHelper.IsLWS */
const isLWS = (c: number) => c <= 0x20 && (c === 0x20 || c === 0x0a || c === 0x0d || c === 0x09);

/** string.Equals / StartsWith with StringComparison.OrdinalIgnoreCase: both sides upper-cased as ordinal casing does. */
function equalsOrdinalIgnoreCase(a: string, b: string): boolean {
    return a.length === b.length && (a === b || toUpperInvariant(a) === toUpperInvariant(b));
}

function startsWithOrdinalIgnoreCase(s: string, prefix: string): boolean {
    return s.length >= prefix.length && equalsOrdinalIgnoreCase(s.substring(0, prefix.length), prefix);
}

/** HexConverter.FromChar */
function fromHexChar(c: number): number {
    if (c >= 0x30 && c <= 0x39) return c - 0x30;
    if (c >= 0x41 && c <= 0x46) return c - 0x41 + 10;
    if (c >= 0x61 && c <= 0x66) return c - 0x61 + 10;
    return 0xff;
}

/** UriHelper.DecodeHexChars: '\uffff' when either is not a hex digit. */
function decodeHexChars(first: number, second: number): number {
    const a = fromHexChar(first);
    const b = fromHexChar(second);
    return (a | b) === 0xff ? NOCHAR : (a << 4) | b;
}

const hexUpper = '0123456789ABCDEF';

/** System.Text.ValueStringBuilder over UTF-16 code units. */
class Vsb {
    chars: number[] = [];

    get length(): number {
        return this.chars.length;
    }

    set length(value: number) {
        this.chars.length = value;
    }

    appendChar(c: number): void {
        this.chars.push(c);
    }

    append(s: string, start = 0, end = s.length): void {
        for (let i = start; i < end; i++) this.chars.push(s.charCodeAt(i));
    }

    appendCodePoint(cp: number): void {
        if (cp >= 0x10000) {
            this.chars.push(0xd800 + ((cp - 0x10000) >> 10), 0xdc00 + ((cp - 0x10000) & 0x3ff));
        } else {
            this.chars.push(cp);
        }
    }

    toString(start = 0): string {
        let result = '';
        for (let i = start; i < this.chars.length; i += 8192) {
            result += String.fromCharCode(...this.chars.slice(i, Math.min(i + 8192, this.chars.length)));
        }

        return result;
    }
}

/** UriHelper.PercentEncodeByte */
function percentEncodeByte(b: number, to: Vsb): void {
    to.appendChar(PERCENT);
    to.appendChar(hexUpper.charCodeAt((b >> 4) & 0xf));
    to.appendChar(hexUpper.charCodeAt(b & 0xf));
}

function utf8Bytes(cp: number): number[] {
    if (cp < 0x80) return [cp];
    if (cp < 0x800) return [0xc0 | (cp >> 6), 0x80 | (cp & 0x3f)];
    if (cp < 0x10000) return [0xe0 | (cp >> 12), 0x80 | ((cp >> 6) & 0x3f), 0x80 | (cp & 0x3f)];
    return [0xf0 | (cp >> 18), 0x80 | ((cp >> 12) & 0x3f), 0x80 | ((cp >> 6) & 0x3f), 0x80 | (cp & 0x3f)];
}

/** Rune.DecodeFromUtf16 at s[i] (before end): the code point and the code units it takes; U+FFFD for a lone surrogate. */
function decodeUtf16(s: string, i: number, end: number): { cp: number; consumed: number } {
    const c = s.charCodeAt(i);
    if (isHighSurrogate(c) && i + 1 < end && isLowSurrogate(s.charCodeAt(i + 1))) {
        return { cp: (c - 0xd800) * 0x400 + (s.charCodeAt(i + 1) - 0xdc00) + 0x10000, consumed: 2 };
    }

    return { cp: isHighSurrogate(c) || isLowSurrogate(c) ? 0xfffd : c, consumed: 1 };
}

/**
 * Rune.DecodeFromUtf8: whether the bytes start with a valid scalar, which, and how many bytes it takes; for invalid or
 * incomplete data, the length of the maximal invalid subsequence (at least one byte).
 */
function decodeUtf8(bytes: readonly number[]): { done: boolean; value: number; consumed: number } {
    const b0 = bytes[0]!;
    if (b0 < 0x80) return { done: true, value: b0, consumed: 1 };
    let count: number, min1: number, max1: number, value: number;
    if (b0 >= 0xc2 && b0 <= 0xdf) {
        count = 2; min1 = 0x80; max1 = 0xbf; value = b0 & 0x1f;
    } else if (b0 >= 0xe0 && b0 <= 0xef) {
        count = 3; min1 = b0 === 0xe0 ? 0xa0 : 0x80; max1 = b0 === 0xed ? 0x9f : 0xbf; value = b0 & 0x0f;
    } else if (b0 >= 0xf0 && b0 <= 0xf4) {
        count = 4; min1 = b0 === 0xf0 ? 0x90 : 0x80; max1 = b0 === 0xf4 ? 0x8f : 0xbf; value = b0 & 0x07;
    } else {
        return { done: false, value: 0, consumed: 1 };
    }

    for (let k = 1; k < count; k++) {
        if (k >= bytes.length) return { done: false, value: 0, consumed: k };
        const b = bytes[k]!;
        if (k === 1 ? b < min1 || b > max1 : b < 0x80 || b > 0xbf) return { done: false, value: 0, consumed: k };
        value = (value << 6) | (b & 0x3f);
    }

    return { done: true, value, consumed: count };
}

// IriHelper

/** IriHelper.CheckIriUnicodeRange(char, bool) */
function checkIriUnicodeRangeChar(unicode: number, isQuery: boolean): boolean {
    if (!(unicode >= 0xa0 && unicode <= 0xd7ff) && !(unicode >= 0xf900 && unicode <= 0xfdcf) && !(unicode >= 0xfdf0 && unicode <= 0xffef)) {
        return isQuery && unicode >= 0xe000 && unicode <= 0xf8ff;
    }

    return true;
}

/** IriHelper.CheckIriUnicodeRange(char, char, out bool, bool): the pair's validity and whether it is a pair. */
function checkIriUnicodeRangePair(highSurr: number, lowSurr: number, isQuery: boolean): { ok: boolean; isSurrogatePair: boolean } {
    if (isHighSurrogate(highSurr) && isLowSurrogate(lowSurr)) {
        const value = (highSurr - 0xd800) * 0x400 + (lowSurr - 0xdc00) + 0x10000;
        if ((value & 0xffff) < 0xfffe && !(value >= 0xe0000 && value <= 0xe0fff)) {
            return { ok: isQuery || value < 0xf0000, isSurrogatePair: true };
        }

        return { ok: false, isSurrogatePair: true };
    }

    return { ok: false, isSurrogatePair: false };
}

/** IriHelper.CheckIriUnicodeRange(uint, bool) */
function checkIriUnicodeRangeValue(value: number, isQuery: boolean): boolean {
    if (value <= 0xffff) return checkIriUnicodeRangeChar(value, isQuery);
    if ((value & 0xffff) < 0xfffe && !(value >= 0xe0000 && value <= 0xe0fff)) return isQuery || value < 0xf0000;
    return false;
}

/** UriHelper.IsGenDelim */
const isGenDelim = (c: number) => c === COLON || c === SLASH || c === QUESTION || c === HASH || c === LBRACKET || c === RBRACKET || c === AT;

/** IriHelper.CheckIsReserved */
function checkIsReserved(c: number, component: number): boolean {
    if ((UriComponents.AbsoluteUri & component) === 0) {
        return component === 0 && isGenDelim(c);
    }

    return c < 128 && ';/?:@&=+$,#[]!\'()*'.includes(String.fromCharCode(c));
}

/** UriHelper.IsNotSafeForUnescape */
function isNotSafeForUnescape(c: number): boolean {
    return c <= 0x1f || (c >= 0x7f && c <= 0x9f) || inTable(notSafeForUnescape, c);
}

/** PercentEncodingHelper's hex digits: the first of a byte above 0x7F (8-9, A-F, a-f), the second any. */
function highHexDigit(c: number): number {
    if ((c >= 0x41 && c <= 0x46) || (c >= 0x61 && c <= 0x66)) return (c | 0x20) - 0x61 + 10;
    if (c === 0x38 || c === 0x39) return c - 0x30;
    return -1;
}

function lowHexDigit(c: number): number {
    if (c >= 0x30 && c <= 0x39) return c - 0x30;
    if ((c >= 0x41 && c <= 0x46) || (c >= 0x61 && c <= 0x66)) return (c | 0x20) - 0x61 + 10;
    return -1;
}

/**
 * PercentEncodingHelper.UnescapePercentEncodedUTF8Sequence over input[start, start + length): reads up to four %XX
 * bytes above 0x7F at a time and decodes them; a valid scalar (in the IRI ranges, when iriParsing) is written as the
 * character, anything else is kept as it was escaped. Returns how many characters of the input it took.
 */
function unescapePercentEncodedUtf8Sequence(input: string, start: number, length: number, dest: Vsb, isQuery: boolean, iriParsing: boolean): number {
    const bytes: number[] = [];
    let consumedChars = 0;
    let pendingInvalid = 0;
    while (true) {
        let position = consumedChars + bytes.length * 3;
        while (bytes.length < 4) {
            if (length - position > 2 && input.charCodeAt(start + position) === PERCENT) {
                const high = highHexDigit(input.charCodeAt(start + position + 1));
                const low = lowHexDigit(input.charCodeAt(start + position + 2));
                if (high >= 0 && low >= 0) {
                    bytes.push((high << 4) | low);
                    position += 3;
                    continue;
                }
            }

            break;
        }

        if (bytes.length < 4 && bytes.length <= 1) {
            if ((bytes.length | pendingInvalid) === 0) return consumedChars;
            const remaining = bytes.length * 3;
            dest.append(input, start + consumedChars - pendingInvalid, start + consumedChars + remaining);
            return consumedChars + remaining;
        }

        const decoded = decodeUtf8(bytes);
        if (decoded.done && (!iriParsing || checkIriUnicodeRangeValue(decoded.value, isQuery))) {
            if (pendingInvalid !== 0) {
                dest.append(input, start + consumedChars - pendingInvalid, start + consumedChars);
                pendingInvalid = 0;
            }

            dest.appendCodePoint(decoded.value);
        } else {
            pendingInvalid += decoded.consumed * 3;
        }

        bytes.splice(0, decoded.consumed);
        consumedChars += decoded.consumed * 3;
    }
}

/** IriHelper.EscapeUnescapeIri */
function escapeUnescapeIri(input: string, start: number, end: number, component: number): string {
    const to = new Vsb();
    for (let i = start; i < end; i++) {
        let c = input.charCodeAt(i);
        if (c === PERCENT) {
            if (end - i > 2) {
                c = decodeHexChars(input.charCodeAt(i + 1), input.charCodeAt(i + 2));
                if (c === NOCHAR || c === PERCENT || checkIsReserved(c, component) || isNotSafeForUnescape(c)) {
                    to.appendChar(input.charCodeAt(i++));
                    to.appendChar(input.charCodeAt(i++));
                    to.appendChar(input.charCodeAt(i));
                } else if (c <= 0x7f) {
                    to.appendChar(c);
                    i += 2;
                } else {
                    const consumed = unescapePercentEncodedUtf8Sequence(input, i, end - i, to, component === UriComponents.Query, true);
                    i += consumed - 1;
                }
            } else {
                to.appendChar(input.charCodeAt(i));
            }
        } else if (c > 0x7f) {
            let isSurrogatePair = false;
            let c2 = 0;
            let ok: boolean;
            if (isHighSurrogate(c) && i + 1 < end) {
                c2 = input.charCodeAt(i + 1);
                const check = checkIriUnicodeRangePair(c, c2, component === UriComponents.Query);
                ok = check.ok;
                isSurrogatePair = check.isSurrogatePair;
            } else {
                ok = checkIriUnicodeRangeChar(c, component === UriComponents.Query);
            }

            if (ok) {
                to.appendChar(c);
                if (isSurrogatePair) to.appendChar(c2);
            } else {
                const cp = isSurrogatePair ? (c - 0xd800) * 0x400 + (c2 - 0xdc00) + 0x10000 : isHighSurrogate(c) || isLowSurrogate(c) ? 0xfffd : c;
                for (const b of utf8Bytes(cp)) percentEncodeByte(b, to);
            }

            if (isSurrogatePair) i++;
        } else {
            to.appendChar(input.charCodeAt(i));
        }
    }

    return to.toString();
}

// UriHelper escaping and unescaping.

/** UriHelper.EscapeStringToBuilder over s[start, end). */
function escapeStringToBuilder(s: string, start: number, end: number, vsb: Vsb, noEscape: boolean[], checkExistingEscaped: boolean): void {
    let i = start;
    while (i < end) {
        const c = s.charCodeAt(i);
        if (c >= 0x80) {
            const { cp, consumed } = decodeUtf16(s, i, end);
            i += consumed;
            for (const b of utf8Bytes(cp)) percentEncodeByte(b, vsb);
        } else if (!noEscape[c]) {
            if (c === PERCENT && checkExistingEscaped && end - i > 2 && isAsciiHexDigit(s.charCodeAt(i + 1)) && isAsciiHexDigit(s.charCodeAt(i + 2))) {
                vsb.append(s, i, i + 3);
                i += 3;
            } else {
                percentEncodeByte(c, vsb);
                i++;
            }
        } else {
            let next = i;
            while (next < end && inTable(noEscape, s.charCodeAt(next))) next++;
            vsb.append(s, i, next);
            i = next;
        }
    }
}

function indexOfAnyExcept(s: string, start: number, end: number, noEscape: boolean[]): number {
    for (let i = start; i < end; i++) {
        if (!inTable(noEscape, s.charCodeAt(i))) return i;
    }

    return -1;
}

/** UriHelper.EscapeString(string, bool, SearchValues): the string itself when nothing is to escape. */
function escapeString(s: string, checkExistingEscaped: boolean, noEscape: boolean[]): string {
    const first = indexOfAnyExcept(s, 0, s.length, noEscape);
    if (first < 0) return s;
    const vsb = new Vsb();
    escapeStringToBuilder(s, first, s.length, vsb, noEscape, checkExistingEscaped);
    return s.substring(0, first) + vsb.toString();
}

/** UriHelper.EscapeString(ReadOnlySpan, ref ValueStringBuilder, bool, SearchValues) over s[start, end). */
function escapeStringInto(s: string, start: number, end: number, dest: Vsb, checkExistingEscaped: boolean, noEscape: boolean[]): void {
    const first = indexOfAnyExcept(s, start, end, noEscape);
    if (first < 0) {
        dest.append(s, start, end);
        return;
    }

    dest.append(s, start, first);
    escapeStringToBuilder(s, first, end, dest, noEscape, checkExistingEscaped);
}

/** UriHelper.UnescapeString over s[start, end). */
function unescapeString(s: string, start: number, end: number, dest: Vsb, rsvd1: number, rsvd2: number, rsvd3: number, unescapeMode: number,
    syntax: UriParser | null, isQuery: boolean): void {
    if ((unescapeMode & M.EscapeUnescape) === 0) {
        dest.append(s, start, end);
        return;
    }

    let escape = false;
    const iri = iriParsingStatic(syntax) && (unescapeMode & M.EscapeUnescape) === M.EscapeUnescape;
    let i = start;
    while (i < end) {
        let c = 0;
        for (; i < end; i++) {
            c = s.charCodeAt(i);
            if (c === PERCENT) {
                if ((unescapeMode & M.Unescape) === 0) {
                    escape = true;
                    break;
                }

                if (i + 2 < end) {
                    c = decodeHexChars(s.charCodeAt(i + 1), s.charCodeAt(i + 2));
                    if (unescapeMode < M.UnescapeAll) {
                        if (c === NOCHAR) {
                            if ((unescapeMode & M.Escape) === 0) continue;
                            escape = true;
                        } else if (c === PERCENT) {
                            i += 2;
                            continue;
                        } else if (c === rsvd1 || c === rsvd2 || c === rsvd3) {
                            i += 2;
                            continue;
                        } else if ((unescapeMode & M.V1ToStringFlag) === 0 && isNotSafeForUnescape(c)) {
                            i += 2;
                            continue;
                        } else if (iri && ((c <= 0x9f && isNotSafeForUnescape(c)) || (c > 0x9f && !checkIriUnicodeRangeChar(c, isQuery)))) {
                            i += 2;
                            continue;
                        }

                        break;
                    }

                    if (c !== NOCHAR) break;
                } else if (unescapeMode < M.UnescapeAll) {
                    escape = true;
                    break;
                }
            } else if ((unescapeMode & (M.Unescape | M.UnescapeAll)) !== (M.Unescape | M.UnescapeAll) && (unescapeMode & M.Escape) !== 0) {
                if (c === rsvd1 || c === rsvd2 || c === rsvd3) {
                    escape = true;
                    break;
                }

                if ((unescapeMode & M.V1ToStringFlag) === 0 && (c <= 0x1f || (c >= 0x7f && c <= 0x9f))) {
                    escape = true;
                    break;
                }
            }
        }

        dest.append(s, start, i);
        start = i;
        if (i !== end) {
            if (escape) {
                percentEncodeByte(s.charCodeAt(i) & 0xff, dest);
                escape = false;
                i++;
            } else if (c <= 0x7f) {
                dest.appendChar(c);
                i += 3;
            } else {
                i += unescapePercentEncodedUtf8Sequence(s, i, end - i, dest, isQuery, iri);
            }

            start = i;
        }
    }
}

/** UriHelper.StripBidiControlCharacters */
function stripBidiControlCharacters(s: string): string {
    const isBidi = (c: number) => c >= 0x200e && c <= 0x202e && !(c >= 0x2010 && c <= 0x2029);
    let result = '';
    let changed = false;
    for (let i = 0; i < s.length; i++) {
        const c = s.charCodeAt(i);
        if (isBidi(c)) {
            changed = true;
        } else {
            result += s[i];
        }
    }

    return changed ? result : s;
}

function iriParsingStatic(syntax: UriParser | null): boolean {
    return syntax?.inFact(S.AllowIriParsing) ?? true;
}

// DomainNameHelper, IPv4AddressHelper, IPv6AddressHelper, UncNameHelper.

/** DomainNameHelper.ParseCanonicalName */
function domainParseCanonicalName(str: string, start: number, end: number, loopback: { value: boolean }): string {
    let num = -1;
    for (let k = end - 1; k >= start; k--) {
        const c = str.charCodeAt(k);
        if ((c >= 0x41 && c <= 0x5a) || c === COLON) {
            num = k - start;
            break;
        }
    }

    if (num >= 0 && str.charCodeAt(start + num) === COLON) {
        end = start + num;
        num = -1;
        for (let k = start; k < end; k++) {
            const c = str.charCodeAt(k);
            if (c >= 0x41 && c <= 0x5a) {
                num = k - start;
                break;
            }
        }
    }

    const span = str.substring(start, end);
    if (num >= 0) {
        if (equalsOrdinalIgnoreCase(span, 'localhost') || equalsOrdinalIgnoreCase(span, 'loopback')) {
            loopback.value = true;
            return 'localhost';
        }

        return toLowerInvariant(span);
    }

    if (span === 'localhost' || span === 'loopback') {
        loopback.value = true;
        return 'localhost';
    }

    return span;
}

/** DomainNameHelper.IsValid over a host candidate: whether it is a host name and how long. */
function domainIsValid(hostname: string, iri: boolean, notImplicitFile: boolean): { valid: boolean; length: number } {
    let num = -1;
    for (let k = 0; k < hostname.length; k++) {
        const c = hostname.charCodeAt(k);
        if (iri ? isIriInvalid(c) : !inTable(domainValidChars, c)) {
            num = k;
            break;
        }
    }

    if (num >= 0) {
        const c = hostname.charCodeAt(num);
        if (!(c === SLASH || c === BACKSLASH || (notImplicitFile && (c === HASH || c === COLON || c === QUESTION)))) {
            return { valid: false, length: 0 };
        }

        hostname = hostname.substring(0, num);
    }

    const length = hostname.length;
    if (length === 0) return { valid: false, length };
    do {
        const c2 = hostname.charCodeAt(0);
        if ((!iri || c2 < 0xa0) && !isAsciiLetterOrDigit(c2)) return { valid: false, length };
        let num2 = -1;
        for (let k = 0; k < hostname.length; k++) {
            const c = hostname.charCodeAt(k);
            if (iri ? isDotLike(c) : c === DOT) {
                num2 = k;
                break;
            }
        }

        let num3 = num2 < 0 ? hostname.length : num2;
        if (iri) {
            const label = hostname.substring(0, num3);
            if (/[^\0-\x7f]/.test(label)) {
                num3 += 4;
                for (let k = 0; k < label.length; k++) {
                    if (label.charCodeAt(k) > 0xff) num3++;
                }
            }
        }

        if (!(num3 >= 1 && num3 <= 63)) return { valid: false, length };
        if (num2 < 0) return { valid: true, length };
        hostname = hostname.substring(num2 + 1);
    } while (hostname.length > 0);

    return { valid: true, length };
}

/** IPv4AddressHelper.ParseNonCanonical: the address, or -1; `end` moves to the end of the address (also when it fails late). */
function ipv4ParseNonCanonical(name: string, start: number, end: { value: number }, notImplicitFile: boolean): number {
    let radix = 10;
    const parts = [0, 0, 0, 0];
    let current = 0;
    let atLeastOneChar = false;
    let dotCount = 0;
    let i: number;
    const stop = end.value;
    for (i = start; i < stop; i++) {
        let c = name.charCodeAt(i);
        current = 0;
        radix = 10;
        if (c === 0x30) {
            radix = 8;
            i++;
            atLeastOneChar = true;
            if (i < stop) {
                c = name.charCodeAt(i);
                if (c === 0x78 || c === 0x58) {
                    radix = 16;
                    i++;
                    atLeastOneChar = false;
                }
            }
        }

        for (; i < stop; i++) {
            c = name.charCodeAt(i);
            let digit: number;
            if ((radix === 10 || radix === 16) && isAsciiDigit(c)) {
                digit = c - 0x30;
            } else if (radix === 8 && c >= 0x30 && c <= 0x37) {
                digit = c - 0x30;
            } else if (radix === 16 && c >= 0x61 && c <= 0x66) {
                digit = c + 10 - 0x61;
            } else if (radix === 16 && c >= 0x41 && c <= 0x46) {
                digit = c + 10 - 0x41;
            } else {
                break;
            }

            current = current * radix + digit;
            if (current > 0xffffffff) return -1;
            atLeastOneChar = true;
        }

        if (i >= stop || name.charCodeAt(i) !== DOT) break;
        if (dotCount >= 3 || !atLeastOneChar || current > 255) return -1;
        parts[dotCount] = current;
        dotCount++;
        atLeastOneChar = false;
    }

    if (!atLeastOneChar) return -1;
    if (i < stop) {
        const c = name.charCodeAt(i);
        if (c !== SLASH && c !== BACKSLASH && (!notImplicitFile || (c !== COLON && c !== QUESTION && c !== HASH))) return -1;
        end.value = i;
    }

    parts[dotCount] = current;
    switch (dotCount) {
        case 0:
            return parts[0]! > 0xffffffff ? -1 : parts[0]!;
        case 1:
            return parts[1]! > 0xffffff ? -1 : parts[0]! * 0x1000000 + (parts[1]! & 0xffffff);
        case 2:
            return parts[2]! > 0xffff ? -1 : parts[0]! * 0x1000000 + (parts[1]! & 0xff) * 0x10000 + (parts[2]! & 0xffff);
        case 3:
            return parts[3]! > 0xff ? -1 : parts[0]! * 0x1000000 + (parts[1]! & 0xff) * 0x10000 + (parts[2]! & 0xff) * 0x100 + (parts[3]! & 0xff);
        default:
            return -1;
    }
}

/** IPv4AddressHelper.IsValidCanonical */
function ipv4IsValidCanonical(name: string, start: number, end: { value: number }, allowIPv6: boolean, notImplicitFile: boolean): boolean {
    let dots = 0;
    let number = 0;
    let haveNumber = false;
    let firstCharIsZero = false;
    while (start < end.value) {
        const c = pc(name, start);
        if (allowIPv6) {
            if (c === RBRACKET || c === SLASH || c === PERCENT) break;
        } else if (c === SLASH || c === BACKSLASH || (notImplicitFile && (c === COLON || c === QUESTION || c === HASH))) {
            break;
        }

        if (isAsciiDigit(c)) {
            if (!haveNumber && c === 0x30) {
                if (start + 1 < end.value && pc(name, start + 1) === 0x30) return false;
                firstCharIsZero = true;
            }

            haveNumber = true;
            number = number * 10 + (c - 0x30);
            if (number > 255) return false;
        } else {
            if (c !== DOT) return false;
            if (!haveNumber || (number > 0 && firstCharIsZero)) return false;
            dots++;
            haveNumber = false;
            number = 0;
            firstCharIsZero = false;
        }

        start++;
    }

    const valid = dots === 3 && haveNumber;
    if (valid) end.value = start;
    return valid;
}

/** IPv4AddressHelper.IsValid */
function ipv4IsValid(name: string, start: number, end: { value: number }, allowIPv6: boolean, notImplicitFile: boolean, unknownScheme: boolean): boolean {
    if (allowIPv6 || unknownScheme) return ipv4IsValidCanonical(name, start, end, allowIPv6, notImplicitFile);
    return ipv4ParseNonCanonical(name, start, end, notImplicitFile) !== -1;
}

/** IPv4AddressHelper.ParseCanonicalName */
function ipv4ParseCanonicalName(str: string, start: number, end: number, loopback: { value: boolean }): string {
    const address = ipv4ParseNonCanonical(str, start, { value: end }, true);
    const bytes = [Math.floor(address / 0x1000000) & 0xff, (address >>> 16) & 0xff, (address >>> 8) & 0xff, address & 0xff];
    loopback.value = bytes[0] === 127;
    return bytes.join('.');
}

/** IPv4AddressHelper.ParseHostNumber */
function ipv4ParseHostNumber(str: string, start: number, end: number): number {
    const bytes = [0, 0, 0, 0];
    for (let i = 0; i < 4; i++) {
        let number = 0;
        let c: number;
        while (start < end && (c = ch(str, start)) !== DOT && c !== COLON) {
            number = number * 10 + c - 0x30;
            start++;
        }

        bytes[i] = number & 0xff;
        start++;
    }

    return (bytes[0]! << 24) | (bytes[1]! << 16) | (bytes[2]! << 8) | bytes[3]!;
}

/** IPv6AddressHelper.IsValid (InternalIsValid, not strict): `end` moves past the ']' when valid. */
function ipv6IsValid(name: string, start: number, end: { value: number }): boolean {
    let sequenceCount = 0;
    let sequenceLength = 0;
    let haveCompressor = false;
    let haveIPv4Address = false;
    let havePrefix = false;
    let expectingNumber = true;
    let lastSequence = 1;
    if (pc(name, start) === COLON && (start + 1 >= end.value || pc(name, start + 1) !== COLON)) return false;
    let i: number;
    for (i = start; i < end.value; i++) {
        if (havePrefix ? isAsciiDigit(pc(name, i)) : isAsciiHexDigit(pc(name, i))) {
            sequenceLength++;
            expectingNumber = false;
            continue;
        }

        if (sequenceLength > 4) return false;
        if (sequenceLength !== 0) {
            sequenceCount++;
            lastSequence = i - sequenceLength;
        }

        const c = pc(name, i);
        let action: 'next' | 'prefix' | 'end';
        if (c <= 46) {
            if (c === PERCENT) {
                action = 'end';
                let reachedEnd = false;
                while (true) {
                    if (++i === end.value) return false;
                    if (pc(name, i) === RBRACKET) {
                        reachedEnd = true;
                        break;
                    }

                    if (pc(name, i) !== SLASH) continue;
                    break;
                }

                if (!reachedEnd) action = 'prefix';
            } else if (c !== DOT) {
                return false;
            } else {
                if (haveIPv4Address) return false;
                const ipv4End = { value: end.value };
                if (!ipv4IsValid(name, lastSequence, ipv4End, true, false, false)) return false;
                i = ipv4End.value;
                sequenceCount++;
                haveIPv4Address = true;
                i--;
                action = 'next';
            }
        } else if (c === SLASH) {
            action = 'prefix';
        } else if (c !== COLON) {
            if (c !== RBRACKET) return false;
            action = 'end';
        } else {
            if (i > 0 && pc(name, i - 1) === COLON) {
                if (haveCompressor) return false;
                haveCompressor = true;
                expectingNumber = false;
            } else {
                expectingNumber = true;
            }

            action = 'next';
        }

        if (action === 'end') {
            start = i;
            i = end.value;
            continue;
        }

        if (action === 'prefix') {
            if (sequenceCount === 0 || havePrefix) return false;
            havePrefix = true;
            expectingNumber = true;
        }

        sequenceLength = 0;
    }

    if (havePrefix && (sequenceLength < 1 || sequenceLength > 2)) return false;
    const expectedSequenceCount = 8 + (havePrefix ? 1 : 0);
    if (!expectingNumber && sequenceLength <= 4 && (haveCompressor ? sequenceCount < expectedSequenceCount : sequenceCount === expectedSequenceCount)) {
        if (i === end.value + 1) {
            end.value = start + 1;
            return true;
        }

        return false;
    }

    return false;
}

/** IPv6AddressHelper.Parse */
function ipv6Parse(address: string, numbers: number[], start: number, scopeId: { value: string | null }): void {
    let number = 0;
    let index = 0;
    let compressorIndex = -1;
    let numberIsValid = true;
    if (ch(address, start) === LBRACKET) start++;
    let i = start;
    while (i < address.length && ch(address, i) !== RBRACKET) {
        switch (ch(address, i)) {
            case PERCENT: {
                if (numberIsValid) {
                    numbers[index++] = number & 0xffff;
                    numberIsValid = false;
                }

                start = i;
                for (i++; i < address.length && ch(address, i) !== RBRACKET && ch(address, i) !== SLASH; i++) { /* the scope id */ }
                scopeId.value = address.substring(start, i);
                for (; i < address.length && ch(address, i) !== RBRACKET; i++) { /* the prefix */ }
                break;
            }
            case COLON: {
                numbers[index++] = number & 0xffff;
                number = 0;
                i++;
                if (ch(address, i) === COLON) {
                    compressorIndex = index;
                    i++;
                } else if (compressorIndex < 0 && index < 6) {
                    break;
                }

                for (let j = i; j < address.length && ch(address, j) !== RBRACKET && ch(address, j) !== COLON && ch(address, j) !== PERCENT && ch(address, j) !== SLASH && j < i + 4; j++) {
                    if (ch(address, j) === DOT) {
                        for (; j < address.length && ch(address, j) !== RBRACKET && ch(address, j) !== SLASH && ch(address, j) !== PERCENT; j++) { /* the IPv4 part */ }
                        number = ipv4ParseHostNumber(address, i, j);
                        numbers[index++] = (number >>> 16) & 0xffff;
                        numbers[index++] = number & 0xffff;
                        i = j;
                        number = 0;
                        numberIsValid = false;
                        break;
                    }
                }

                break;
            }
            case SLASH: {
                if (numberIsValid) {
                    numbers[index++] = number & 0xffff;
                    numberIsValid = false;
                }

                for (i++; ch(address, i) !== RBRACKET; i++) { /* the prefix length */ }
                break;
            }
            default: {
                const digit = fromHexChar(ch(address, i++));
                if (digit === 0xff) throw new Error('digit');
                number = number * 16 + digit;
                break;
            }
        }
    }

    if (numberIsValid) numbers[index++] = number & 0xffff;
    if (compressorIndex <= 0) return;
    let toIndex = 7;
    let fromIndex = index - 1;
    if (fromIndex !== toIndex) {
        for (let k = index - compressorIndex; k > 0; k--) {
            numbers[toIndex--] = numbers[fromIndex]!;
            numbers[fromIndex--] = 0;
        }
    }
}

/** IPv6AddressHelper.ParseCanonicalName: "[...]", compressed, lower-case hex. */
function ipv6ParseCanonicalName(str: string, start: number, loopback: { value: boolean }, scopeId: { value: string | null }): string {
    const numbers = [0, 0, 0, 0, 0, 0, 0, 0];
    ipv6Parse(str, numbers, start, scopeId);
    loopback.value = numbers[0] === 0 && numbers[1] === 0 && numbers[2] === 0 && numbers[3] === 0 && numbers[4] === 0
        && ((numbers[5] === 0 && numbers[6] === 0 && numbers[7] === 1)
            || (numbers[6] === 0x7f00 && numbers[7] === 1 && (numbers[5] === 0 || numbers[5] === 0xffff)));

    let longestLength = 0;
    let longestStart = -1;
    let current = 0;
    for (let i = 0; i < 8; i++) {
        if (numbers[i] === 0) {
            current++;
            if (current > longestLength) {
                longestLength = current;
                longestStart = i - current + 1;
            }
        } else {
            current = 0;
        }
    }

    let compressionStart = -1;
    let compressionEnd = 0;
    if (longestLength > 1) {
        compressionStart = longestStart;
        compressionEnd = longestStart + longestLength;
    }

    let ipv4Embedded = false;
    if (numbers[0] === 0 && numbers[1] === 0 && numbers[2] === 0 && numbers[3] === 0 && numbers[6] !== 0) {
        if (numbers[4] === 0 && (numbers[5] === 0 || numbers[5] === 0xffff)) ipv4Embedded = true;
        else if (numbers[4] === 0xffff && numbers[5] === 0) ipv4Embedded = true;
    }

    if (!ipv4Embedded && numbers[4] === 0) ipv4Embedded = numbers[5] === 0x5efe;

    let result = '[';
    for (let i = 0; i < 8; i++) {
        if (ipv4Embedded && i === 6) {
            result += `:${numbers[i]! >> 8}.${numbers[i]! & 0xff}.${numbers[i + 1]! >> 8}.${numbers[i + 1]! & 0xff}`;
            break;
        }

        if (compressionStart === i) result += ':';
        if (compressionStart <= i && compressionEnd === 8) {
            result += ':';
            break;
        }

        if (compressionStart > i || i >= compressionEnd) {
            if (i !== 0) result += ':';
            result += numbers[i]!.toString(16);
        }
    }

    return result + ']';
}

/** UncNameHelper.IsValid: `returnedEnd` moves to the end of the name when valid. */
function uncIsValid(name: string, start: number, returnedEnd: { value: number }, notImplicitFile: boolean): boolean {
    let end = returnedEnd.value;
    if (start === end) return false;
    let validSoFar = false;
    let i: number;
    for (i = start; i < end; i++) {
        const c = pc(name, i);
        if (c === SLASH || c === BACKSLASH || (notImplicitFile && (c === COLON || c === QUESTION || c === HASH))) {
            end = i;
            break;
        }

        if (c === DOT) {
            i++;
            break;
        }

        if (isLetter(c) || c === 0x2d || c === 0x5f) {
            validSoFar = true;
        } else if (!isAsciiDigit(c)) {
            return false;
        }
    }

    if (!validSoFar) return false;
    for (; i < end; i++) {
        const c = pc(name, i);
        if (c === SLASH || c === BACKSLASH || (notImplicitFile && (c === COLON || c === QUESTION || c === HASH))) {
            end = i;
            break;
        }

        if (c === DOT) {
            if (!validSoFar || (i - 1 >= start && pc(name, i - 1) === DOT)) return false;
            validSoFar = false;
            continue;
        }

        if (c === 0x2d || c === 0x5f) {
            if (!validSoFar) return false;
            continue;
        }

        if (isLetter(c) || isAsciiDigit(c)) {
            if (!validSoFar) validSoFar = true;
            continue;
        }

        return false;
    }

    if (i - 1 >= start && pc(name, i - 1) === DOT) validSoFar = true;
    if (!validSoFar) return false;
    returnedEnd.value = end;
    return true;
}

// Uri.

interface Offset {
    scheme: number;
    user: number;
    host: number;
    portValue: number;
    path: number;
    query: number;
    fragment: number;
    end: number;
}

interface UriInfo {
    offset: Offset;
    string: string | null;
    host: string | null;
    scopeId: { value: string | null };
    remoteUrl: string | null;
    absoluteUri: string | null;
}

interface Ref<T> {
    value: T;
}

/** Uri.CheckSchemeSyntax */
function checkSchemeSyntax(scheme: string, error: Ref<ParsingError>): UriParser | null {
    const is = (name: string) => equalsOrdinalIgnoreCase(scheme, name);
    switch (scheme.length) {
        case 2:
            if (is('ws')) return WsUri;
            break;
        case 3:
            if (is('wss')) return WssUri;
            if (is('ftp')) return FtpUri;
            break;
        case 4:
            if (is('http')) return HttpUri;
            if (is('file')) return FileUri;
            if (is('uuid')) return UuidUri;
            if (is('nntp')) return NntpUri;
            if (is('ldap')) return LdapUri;
            if (is('news')) return NewsUri;
            break;
        case 5:
            if (is('https')) return HttpsUri;
            break;
        case 6:
            if (is('mailto')) return MailToUri;
            if (is('gopher')) return GopherUri;
            if (is('telnet')) return TelnetUri;
            break;
        case 7:
            if (is('net.tcp')) return NetTcpUri;
            break;
        case 8:
            if (is('net.pipe')) return NetPipeUri;
            break;
    }

    if (scheme.length === 0 || !isAsciiLetter(scheme.charCodeAt(0)) || indexOfAnyExcept(scheme, 0, scheme.length, schemeChars) >= 0) {
        error.value = ParsingError.BadScheme;
        return null;
    }

    if (scheme.length > 1024) {
        error.value = ParsingError.SchemeLimit;
        return null;
    }

    return findOrFetchAsUnknownV1Syntax(toLowerInvariant(scheme));
}

interface SchemeState {
    flags: bigint;
    syntax: UriParser | null;
}

/** Uri.ParseSchemeCheckImplicitFile */
function parseSchemeCheckImplicitFile(uriString: string, err: Ref<ParsingError>, state: SchemeState): number {
    let i: number;
    for (i = 0; i < uriString.length && isLWS(uriString.charCodeAt(i)); i++) { /* the leading white space */ }
    let unixPath = i < uriString.length && uriString.charCodeAt(i) === SLASH;
    if (unixPath) {
        let noSecondSlash = i + 1 >= uriString.length;
        if (!noSecondSlash) {
            const c = uriString.charCodeAt(i + 1);
            noSecondSlash = !(c === SLASH || c === BACKSLASH);
        }

        unixPath = noSecondSlash;
    }

    if (unixPath) {
        state.flags |= F.AuthorityFound | F.ImplicitFile | F.UnixPath;
        state.syntax = UnixFileUri;
        return i;
    }

    const colon = uriString.indexOf(':', i);
    const num = colon < 0 ? -1 : colon - i;
    if (i + 2 >= uriString.length || num === 0 || i >= uriString.length || i + 1 >= uriString.length) {
        err.value = ParsingError.BadFormat;
        return 0;
    }

    let c = uriString.charCodeAt(i + 1);
    if (c === COLON || c === PIPE) {
        if (isAsciiLetter(uriString.charCodeAt(i))) {
            c = uriString.charCodeAt(i + 2);
            if (c === SLASH || c === BACKSLASH) {
                state.flags |= F.AuthorityFound | F.DosPath | F.ImplicitFile;
                state.syntax = FileUri;
                return i;
            }

            err.value = ParsingError.MustRootedPath;
            return 0;
        }

        err.value = uriString.charCodeAt(i + 1) !== COLON ? ParsingError.BadFormat : ParsingError.BadScheme;
        return 0;
    }

    c = uriString.charCodeAt(i);
    if (c === SLASH || c === BACKSLASH) {
        c = uriString.charCodeAt(i + 1);
        if (c === SLASH || c === BACKSLASH) {
            state.flags |= F.AuthorityFound | F.UncPath | F.ImplicitFile;
            state.syntax = FileUri;
            i += 2;
            while (i < uriString.length && (uriString.charCodeAt(i) === SLASH || uriString.charCodeAt(i) === BACKSLASH)) i++;
            return i;
        }

        err.value = ParsingError.BadFormat;
        return 0;
    }

    if (num < 0) {
        err.value = ParsingError.BadFormat;
        return 0;
    }

    state.syntax = checkSchemeSyntax(uriString.substring(i, i + num), err);
    if (state.syntax === null) return 0;
    return i + num + 1;
}

/** Uri.ParseScheme */
function parseScheme(uriString: string, state: SchemeState): ParsingError {
    const length = uriString.length;
    if (length === 0) return ParsingError.EmptyUriString;
    if (length >= 65520) return ParsingError.SizeLimit;
    if (startsWithOrdinalIgnoreCase(uriString, 'https:')) {
        state.syntax = HttpsUri;
        state.flags |= F.UserNotCanonical | F.HostNotCanonical;
    } else if (startsWithOrdinalIgnoreCase(uriString, 'http:')) {
        state.syntax = HttpUri;
        state.flags |= F.SchemeNotCanonical | F.HostNotCanonical;
    } else {
        const err: Ref<ParsingError> = { value: ParsingError.None };
        const num = parseSchemeCheckImplicitFile(uriString, err, state);
        if (err.value !== ParsingError.None) return err.value;
        state.flags |= BigInt(num);
    }

    return ParsingError.None;
}

/** Uri.CheckForUnicodeOrEscapedUnreserved */
function checkForUnicodeOrEscapedUnreserved(data: string): boolean {
    for (let i = 0; i < data.length; i++) {
        const c = data.charCodeAt(i);
        if (c === PERCENT) {
            if (i + 2 < data.length) {
                const c2 = decodeHexChars(data.charCodeAt(i + 1), data.charCodeAt(i + 2));
                if (c2 > 0x7f || Unreserved[c2]) return true;
                i += 2;
            }
        } else if (c > 0x7f) {
            return true;
        }
    }

    return false;
}

/** Uri.Compress over a[start, start + length): the new length. */
function compress(a: number[], start: number, length: number, syntax: UriParser): number {
    if (syntax.inFact(S.ConvertPathSlashes)) {
        for (let k = start; k < start + length; k++) {
            if (a[k] === BACKSLASH) a[k] = SLASH;
        }
    }

    let slashCount = 0;
    let lastSlash = 0;
    let dotCount = 0;
    let removeSegments = 0;
    let len = length;
    const copy = (from: number, to: number) => a.copyWithin(start + to, start + from, start + len);
    for (let i = len - 1; i >= 0; i--) {
        const c = a[start + i]!;
        if (c === SLASH) {
            slashCount++;
        } else {
            if (slashCount > 1) lastSlash = i + 1;
            slashCount = 0;
        }

        if (c === DOT) {
            dotCount++;
            continue;
        }

        if (dotCount !== 0) {
            if ((!syntax.notAny(S.CanonicalizeAsFilePath) || (dotCount <= 2 && c === SLASH)) && c === SLASH
                && (lastSlash === i + dotCount + 1 || (lastSlash === 0 && i + dotCount + 1 === len)) && dotCount <= 2) {
                lastSlash = i + 1 + dotCount + (lastSlash !== 0 ? 1 : 0);
                copy(lastSlash, i + 1);
                len -= lastSlash - i - 1;
                lastSlash = i;
                if (dotCount === 2) removeSegments++;
                dotCount = 0;
                continue;
            }

            dotCount = 0;
        }

        if (c === SLASH) {
            if (removeSegments !== 0) {
                removeSegments--;
                copy(lastSlash + 1, i + 1);
                len -= lastSlash - i;
            }

            lastSlash = i;
        }
    }

    if (len !== 0 && syntax.inFact(S.CanonicalizeAsFilePath) && slashCount <= 1) {
        if (removeSegments !== 0 && a[start] !== SLASH) {
            lastSlash++;
            copy(lastSlash, 0);
            return len - lastSlash;
        }

        if (dotCount !== 0 && (lastSlash === dotCount || (lastSlash === 0 && dotCount === len))) {
            dotCount += lastSlash !== 0 ? 1 : 0;
            copy(dotCount, 0);
            return len - dotCount;
        }
    }

    return len;
}

/** Uri.UnescapeOnly over a[start, end): unescapes %XX of the three characters in place; the new end. */
function unescapeOnly(a: number[], start: number, end: number, ch1: number, ch2: number, ch3: number): number {
    if (end - start < 3) return end;
    let last = end - 2;
    let p = start;
    let q = -1;
    while (p < last) {
        if (a[p++] !== PERCENT) continue;
        let c = decodeHexChars(a[p++]!, a[p++]!);
        if (c !== ch1 && c !== ch2 && c !== ch3) continue;
        q = p - 2;
        a[q - 1] = c;
        while (p < last) {
            if ((a[q++] = a[p++]!) === PERCENT) {
                c = decodeHexChars(a[q++] = a[p++]!, a[q++] = a[p++]!);
                if (c === ch1 || c === ch2 || c === ch3) {
                    q -= 2;
                    a[q - 1] = c;
                }
            }
        }

        break;
    }

    last += 2;
    if (q === -1) return end;
    if (p === last) return end - (p - q);
    a[q++] = a[p++]!;
    if (p === last) return end - (p - q);
    a[q++] = a[p++]!;
    return end - (p - q);
}

const creating = Symbol('creating');

export class Uri {
    private _string!: string;
    private _originalUnicodeString!: string | null;
    private _syntax!: UriParser | null;
    private _flags!: bigint;
    private _info!: UriInfo | null;

    /** new Uri(string uriString, UriKind uriKind = Absolute): throws UriFormatException. */
    constructor(uriString: string, uriKind?: UriKind);
    /** new Uri(Uri baseUri, string? relativeUri) */
    constructor(baseUri: Uri, relativeUri: string | null);
    constructor(first: string | Uri | typeof creating, second?: UriKind | string | null) {
        this._string = '';
        this._originalUnicodeString = null;
        this._syntax = null;
        this._flags = F.Zero;
        this._info = null;
        if (first === creating) return;
        if (first instanceof Uri) {
            if (!first.isAbsoluteUri) throw new ArgumentOutOfRangeException('baseUri');
            this.createUri(first, (second as string | null | undefined) ?? null, false);
            return;
        }

        this.createThis(first, false, (second as UriKind | undefined) ?? UriKind.Absolute);
    }

    /** Uri.TryCreate(string?, UriKind, out Uri?) */
    static tryCreate(uriString: string | null, uriKind: UriKind): Uri | null {
        if (uriString === null) return null;
        const e: Ref<UriFormatException | null> = { value: null };
        const result = Uri.createHelper(uriString, false, uriKind, e);
        return e.value === null ? result : null;
    }

    /** Uri.EscapeDataString: all but the unreserved characters percent-encoded as UTF-8 (a lone surrogate as U+FFFD). */
    static escapeDataString(stringToEscape: string): string {
        return escapeString(stringToEscape, false, Unreserved);
    }

    /** operator ==(Uri?, Uri?) */
    static equals(uri1: Uri | null, uri2: Uri | null): boolean {
        if (uri1 === uri2) return true;
        if (uri1 === null || uri2 === null) return false;
        return uri1.equals(uri2);
    }

    get originalString(): string {
        return this._originalUnicodeString ?? this._string;
    }

    get isAbsoluteUri(): boolean {
        return this._syntax !== null;
    }

    get host(): string {
        if (this.isNotAbsoluteUri) throw new InvalidOperationException(messages.notAbsolute);
        return this.getParts(UriComponents.Host, UriFormat.UriEscaped);
    }

    get absoluteUri(): string {
        if (this._syntax === null) throw new InvalidOperationException(messages.notAbsolute);
        const info = this.ensureUriInfo();
        return info.absoluteUri ??= this.getParts(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
    }

    get port(): number {
        if (this.isNotAbsoluteUri) throw new InvalidOperationException(messages.notAbsolute);
        this.ensureUriInfo();
        if (this.inFact(F.NotDefaultPort)) return this._info!.offset.portValue;
        return this._syntax!.defaultPort;
    }

    get userEscaped(): boolean {
        return this.inFact(F.UserEscaped);
    }

    get isUnc(): boolean {
        if (this.isNotAbsoluteUri) throw new InvalidOperationException(messages.notAbsolute);
        return this.isUncPath;
    }

    get scheme(): string {
        if (this.isNotAbsoluteUri) throw new InvalidOperationException(messages.notAbsolute);
        return this._syntax!.schemeName;
    }

    toString(): string {
        if (this._syntax === null) return this._string;
        const info = this.ensureUriInfo();
        return info.string ??= this.getComponentsHelper(UriComponents.AbsoluteUri, V1ToString);
    }

    getComponents(components: UriComponents, format: UriFormat): string {
        if (this.disablePathAndQueryCanonicalization && (components & UriComponents.PathAndQuery) !== 0) {
            throw new InvalidOperationException('GetComponents() may not be used for Path/Query on a Uri instance created with UriCreationOptions.DangerousDisablePathAndQueryCanonicalization.');
        }

        return this.internalGetComponents(components, format);
    }

    getHashCode(): number {
        if (this.isNotAbsoluteUri) return stringHashCode(this.originalString);
        const info = this.ensureUriInfo();
        let components = UriComponents.HttpRequestUrl;
        if (this._syntax!.inFact(S.MailToLikeUri)) components |= UriComponents.UserInfo;
        const text = info.remoteUrl ??= this.getParts(components, UriFormat.SafeUnescaped);
        return this.isUncOrDosPath ? stringHashCodeOrdinalIgnoreCase(text) : stringHashCode(text);
    }

    /** Equals(Uri?) — what == and the default equality comparer use. */
    equals(other: Uri | null): boolean {
        if (other === null) return false;
        if (this === other) return true;
        if (this.disablePathAndQueryCanonicalization !== other.disablePathAndQueryCanonicalization) return false;
        // Here .NET returns true when both OriginalStrings are the very same string object (a reference comparison
        // JavaScript cannot make). The rest gives the same answer for two Uris made alike from one string; the site
        // makes none from one string object in two ways (each comes from its own JSON value or ToString()).
        if (this.isAbsoluteUri !== other.isAbsoluteUri) return false;
        if (this.isNotAbsoluteUri) return this.originalString === other.originalString;
        if ((this.notAny(F.AllUriInfoSet) || other.notAny(F.AllUriInfoSet))
            && (this.isUncOrDosPath ? equalsOrdinalIgnoreCase(this._string, other._string) : this._string === other._string)) {
            return true;
        }

        this.ensureUriInfo();
        other.ensureUriInfo();
        const info = this._info!;
        const otherInfo = other._info!;
        if (!this.userDrivenParsing && !other.userDrivenParsing && this._syntax!.isSimple && other._syntax!.isSimple) {
            if (this.inFact(F.CanonicalDnsHost) && other.inFact(F.CanonicalDnsHost)) {
                let i = info.offset.host;
                let end = info.offset.path;
                let j = otherInfo.offset.host;
                const otherEnd = otherInfo.offset.path;
                const text = other._string;
                if (end - i > otherEnd - j) end = i + otherEnd - j;
                while (i < end) {
                    if (ch(this._string, i) !== ch(text, j)) return false;
                    if (ch(text, j) === COLON) break;
                    i++;
                    j++;
                }

                if (i < info.offset.path && ch(this._string, i) !== COLON) return false;
                if (j < otherEnd && ch(text, j) !== COLON) return false;
            } else {
                this.ensureHostString(false);
                other.ensureHostString(false);
                if (info.host !== otherInfo.host) return false;
            }

            if (this.port !== other.port) return false;
        }

        let components = UriComponents.HttpRequestUrl;
        if (this._syntax!.inFact(S.MailToLikeUri)) {
            if (!other._syntax!.inFact(S.MailToLikeUri)) return false;
            components |= UriComponents.UserInfo;
        }

        const a = info.remoteUrl ??= this.getParts(components, UriFormat.SafeUnescaped);
        const b = otherInfo.remoteUrl ??= other.getParts(components, UriFormat.SafeUnescaped);
        return this.isUncOrDosPath ? equalsOrdinalIgnoreCase(a, b) : a === b;
    }

    // The private members.

    private get isImplicitFile(): boolean {
        return (this._flags & F.ImplicitFile) !== 0n;
    }

    private get isUncOrDosPath(): boolean {
        return (this._flags & (F.DosPath | F.UncPath)) !== 0n;
    }

    private get isDosPath(): boolean {
        return (this._flags & F.DosPath) !== 0n;
    }

    private get isUncPath(): boolean {
        return (this._flags & F.UncPath) !== 0n;
    }

    private get isUnixPath(): boolean {
        return (this._flags & F.UnixPath) !== 0n;
    }

    private get hostType(): bigint {
        return this._flags & F.HostTypeMask;
    }

    private get isNotAbsoluteUri(): boolean {
        return this._syntax === null;
    }

    private get iriParsing(): boolean {
        return iriParsingStatic(this._syntax);
    }

    private get disablePathAndQueryCanonicalization(): boolean {
        return (this._flags & F.DisablePathAndQueryCanonicalization) !== 0n;
    }

    private get userDrivenParsing(): boolean {
        return (this._flags & F.UserDrivenParsing) !== 0n;
    }

    private get isFile(): boolean {
        return this._syntax!.schemeName === 'file';
    }

    private get securedPathIndex(): number {
        if (this.isDosPath) {
            const c = ch(this._string, this._info!.offset.path);
            return c !== SLASH && c !== BACKSLASH ? 2 : 3;
        }

        return 0;
    }

    private notAny(flags: bigint): boolean {
        return (this._flags & flags) === 0n;
    }

    private inFact(flags: bigint): boolean {
        return (this._flags & flags) !== 0n;
    }

    private ensureUriInfo(): UriInfo {
        if ((this._flags & F.MinimalUriInfoSet) === 0n) this.createUriInfo(this._flags);
        return this._info!;
    }

    private ensureParseRemaining(): void {
        if ((this._flags & F.AllUriInfoSet) === 0n) this.parseRemaining();
    }

    private ensureHostString(allowDnsOptimization: boolean): void {
        if (this.ensureUriInfo().host === null && (!allowDnsOptimization || !this.inFact(F.CanonicalDnsHost))) this.createHostString();
    }

    /** IPv6 DnsSafeHost (IdnHost): the address without the brackets, with the scope id. */
    private get ipv6DnsSafeHost(): string {
        this.ensureHostString(false);
        const text = this._info!.host!;
        const scopeId = this._info!.scopeId.value;
        return scopeId !== null ? text.substring(1, text.length - 1) + scopeId : text.substring(1, text.length - 1);
    }

    private static createRaw(flags: bigint, syntax: UriParser | null, uri: string): Uri {
        const uriObject = new Uri(creating as unknown as string);
        uriObject._flags = flags;
        uriObject._syntax = syntax;
        uriObject._string = uri;
        return uriObject;
    }

    /** Uri.CreateHelper */
    private static createHelper(uriString: string, dontEscape: boolean, uriKind: UriKind, e: Ref<UriFormatException | null>): Uri | null {
        const state: SchemeState = { flags: F.Zero, syntax: null };
        const parsingError = parseScheme(uriString, state);
        if (dontEscape) state.flags |= F.UserEscaped;
        if (parsingError !== ParsingError.None) {
            if (uriKind !== UriKind.Absolute && parsingError <= ParsingError.EmptyUriString) {
                return Uri.createRaw(state.flags & F.UserEscaped, null, uriString);
            }

            return null;
        }

        const uri = Uri.createRaw(state.flags, state.syntax, uriString);
        try {
            uri.initializeUri(parsingError, uriKind, e);
            return e.value === null ? uri : null;
        } catch (ex) {
            if (ex instanceof UriFormatException) {
                e.value = ex;
                return null;
            }

            throw ex;
        }
    }

    /** Uri.CreateThis */
    private createThis(uri: string | null, dontEscape: boolean, uriKind: UriKind): void {
        if (uriKind < UriKind.RelativeOrAbsolute || uriKind > UriKind.Relative) {
            throw new Error(`The value '${uriKind}' passed for the UriKind parameter is invalid.`);
        }

        this._string = uri ?? '';
        if (dontEscape) this._flags |= F.UserEscaped;
        const state: SchemeState = { flags: this._flags, syntax: this._syntax };
        const err = parseScheme(this._string, state);
        this._flags = state.flags;
        this._syntax = state.syntax;
        const e: Ref<UriFormatException | null> = { value: null };
        this.initializeUri(err, uriKind, e);
        if (e.value !== null) throw e.value;
    }

    /** Uri.InitializeUri */
    private initializeUri(err: ParsingError, uriKind: UriKind, e: Ref<UriFormatException | null>): void {
        if (err === ParsingError.None) {
            if (this.isImplicitFile) {
                if (this.notAny(F.DosPath) && uriKind !== UriKind.Absolute
                    && (uriKind === UriKind.Relative || (this._string.length >= 2 && (this._string.charCodeAt(0) !== BACKSLASH || this._string.charCodeAt(1) !== BACKSLASH)) || this.inFact(F.UnixPath))) {
                    this._syntax = null;
                    this._flags &= F.UserEscaped;
                    e.value = null;
                    return;
                }

                if (uriKind === UriKind.Relative && this.inFact(F.DosPath)) {
                    this._syntax = null;
                    this._flags &= F.UserEscaped;
                    e.value = null;
                    return;
                }
            }
        } else if (err > ParsingError.EmptyUriString) {
            this._string = null as unknown as string;
            e.value = getException(err);
            return;
        }

        let hasUnicode = false;
        if (this.iriParsing && checkForUnicodeOrEscapedUnreserved(this._string)) {
            this._flags |= F.HasUnicode;
            hasUnicode = true;
            this._originalUnicodeString = this._string;
        }

        if (this._syntax !== null) {
            // A built-in parser is always simple.
            err = this.privateParseMinimal();
            if (err !== ParsingError.None) {
                if (uriKind !== UriKind.Absolute && err <= ParsingError.EmptyUriString) {
                    this._syntax = null;
                    e.value = null;
                    this._flags &= F.UserEscaped;
                    return;
                }

                e.value = getException(err);
            } else if (uriKind === UriKind.Relative) {
                e.value = getException(ParsingError.CannotCreateRelative);
            } else {
                e.value = null;
            }

            if (hasUnicode) {
                try {
                    this.ensureParseRemaining();
                } catch (ex) {
                    if (!(ex instanceof UriFormatException)) throw ex;
                    e.value = ex;
                }
            }
        } else if (err !== ParsingError.None && uriKind !== UriKind.Absolute && err <= ParsingError.EmptyUriString) {
            e.value = null;
            this._flags &= F.UserEscaped | F.HasUnicode;
            if (hasUnicode) {
                this._string = escapeUnescapeIri(this._originalUnicodeString!, 0, this._originalUnicodeString!.length, 0);
            }
        } else {
            this._string = null as unknown as string;
            e.value = getException(err);
        }
    }

    /** Uri.PrivateParseMinimal */
    private privateParseMinimal(): ParsingError {
        let num = Number(this._flags & F.IndexMask);
        let num2 = this._string.length;
        this._flags &= ~(F.IndexMask | F.UserDrivenParsing);
        const ptr = this._string;
        if (num2 > num && isLWS(pc(ptr, num2 - 1))) {
            num2--;
            while (num2 !== num && isLWS(pc(ptr, --num2))) { /* the trailing white space */ }
            num2++;
        }

        if (this.inFact(F.UnixPath)) {
            this._flags |= F.BasicHostType;
            this._flags |= BigInt(num);
            return ParsingError.None;
        }

        if (this._syntax!.isAllSet(S.AllowEmptyHost | S.AllowDOSPath) && this.notAny(F.ImplicitFile) && num + 1 < num2) {
            let i: number;
            for (i = num; i < num2; i++) {
                const c = pc(ptr, i);
                if (c !== BACKSLASH && c !== SLASH) break;
            }

            if (this._syntax!.inFact(S.FileLikeUri) || i - num <= 3) {
                if (i - num >= 2) this._flags |= F.AuthorityFound;
                let c: number;
                if (i + 1 < num2 && ((c = pc(ptr, i + 1)) === COLON || c === PIPE) && isAsciiLetter(pc(ptr, i))) {
                    if (i + 2 >= num2 || ((c = pc(ptr, i + 2)) !== BACKSLASH && c !== SLASH)) {
                        if (this._syntax!.inFact(S.FileLikeUri)) return ParsingError.MustRootedPath;
                    } else {
                        this._flags |= F.DosPath;
                        if (this._syntax!.inFact(S.MustHaveAuthority)) this._flags |= F.AuthorityFound;
                        num = i === num || i - num === 2 ? i : i - 1;
                    }
                } else if (this._syntax!.inFact(S.FileLikeUri) && i - num >= 2 && i - num !== 3 && i < num2 && pc(ptr, i) !== QUESTION && pc(ptr, i) !== HASH) {
                    this._flags |= F.UncPath;
                    num = i;
                } else if (this._syntax!.inFact(S.FileLikeUri) && pc(ptr, i - 1) === SLASH && i - num === 3) {
                    this._syntax = UnixFileUri;
                    this._flags |= F.AuthorityFound | F.UnixPath;
                    num += 2;
                }
            }
        }

        if ((this._flags & (F.DosPath | F.UncPath | F.UnixPath)) === 0n) {
            if (num + 2 <= num2) {
                const c2 = pc(ptr, num);
                const c3 = pc(ptr, num + 1);
                if (this._syntax!.inFact(S.MustHaveAuthority)) {
                    if ((c2 !== SLASH && c2 !== BACKSLASH) || (c3 !== SLASH && c3 !== BACKSLASH)) return ParsingError.BadAuthority;
                    this._flags |= F.AuthorityFound;
                    num += 2;
                } else if (this._syntax!.inFact(S.OptionalAuthority) && (this.inFact(F.AuthorityFound) || (c2 === SLASH && c3 === SLASH))) {
                    this._flags |= F.AuthorityFound;
                    num += 2;
                } else if (this._syntax!.notAny(S.MailToLikeUri)) {
                    if (this.inFact(F.HasUnicode)) this._string = this._string.substring(0, num);
                    this._flags |= BigInt(num) | F.HostTypeMask;
                    return ParsingError.None;
                }
            } else {
                if (this._syntax!.inFact(S.MustHaveAuthority)) return ParsingError.BadAuthority;
                if (this._syntax!.notAny(S.MailToLikeUri)) {
                    if (this.inFact(F.HasUnicode)) this._string = this._string.substring(0, num);
                    this._flags |= BigInt(num) | F.HostTypeMask;
                    return ParsingError.None;
                }
            }
        }

        if (this.inFact(F.DosPath)) {
            this._flags |= (this._flags & F.AuthorityFound) !== 0n ? F.BasicHostType : F.HostTypeMask;
            this._flags |= BigInt(num);
            return ParsingError.None;
        }

        const err: Ref<ParsingError> = { value: ParsingError.None };
        const flags: Ref<bigint> = { value: this._flags };
        const newHost: Ref<string | null> = { value: null };
        num = this.checkAuthorityHelper(ptr, num, num2, err, flags, this._syntax!, newHost);
        this._flags = flags.value;
        if (err.value !== ParsingError.None) return err.value;
        if (num < num2) {
            const c4 = pc(ptr, num);
            if (c4 === BACKSLASH && this.notAny(F.ImplicitFile) && this._syntax!.notAny(S.AllowDOSPath)) return ParsingError.BadAuthorityTerminator;
            if (c4 === SLASH && this.notAny(F.ImplicitFile) && this.inFact(F.UncPath) && this._syntax === FileUri) this._syntax = UnixFileUri;
        }

        if (newHost.value !== null) this._string = newHost.value;
        this._flags |= BigInt(num);
        return ParsingError.None;
    }

    /** Uri.CreateUriInfo */
    private createUriInfo(cF: bigint): void {
        const info: UriInfo = {
            offset: { scheme: 0, user: 0, host: 0, portValue: 0, path: 0, query: 0, fragment: 0, end: this._string.length & 0xffff },
            string: null,
            host: null,
            scopeId: { value: null },
            remoteUrl: null,
            absoluteUri: null,
        };
        const offset = info.offset;
        if (!this.userDrivenParsing) {
            let backslash = false;
            let i: number;
            if ((cF & F.ImplicitFile) !== 0n) {
                i = 0;
                while (isLWS(ch(this._string, i))) {
                    i++;
                    offset.scheme++;
                }

                if ((cF & F.UncPath) !== 0n) {
                    i += 2;
                    const index = Number(cF & F.IndexMask);
                    for (; i < index && (ch(this._string, i) === SLASH || ch(this._string, i) === BACKSLASH); i++) { /* the slashes */ }
                }
            } else {
                i = this._syntax!.schemeName.length;
                while (ch(this._string, i++) !== COLON) offset.scheme++;
                if ((cF & F.AuthorityFound) !== 0n) {
                    if (ch(this._string, i) === BACKSLASH || ch(this._string, i + 1) === BACKSLASH) backslash = true;
                    i += 2;
                    if ((cF & (F.DosPath | F.UncPath)) !== 0n) {
                        const index = Number(cF & F.IndexMask);
                        for (; i < index && (ch(this._string, i) === SLASH || ch(this._string, i) === BACKSLASH); i++) backslash = true;
                    }
                }
            }

            if (this._syntax!.defaultPort !== -1) offset.portValue = this._syntax!.defaultPort & 0xffff;
            if ((cF & F.HostTypeMask) === F.HostTypeMask || (cF & F.DosPath) !== 0n) {
                offset.user = Number(cF & F.IndexMask);
                offset.host = offset.user;
                offset.path = offset.user;
                cF &= notIndexMask;
                if (backslash) cF |= F.SchemeNotCanonical;
            } else {
                offset.user = i & 0xffff;
                if (this.hostType === F.BasicHostType) {
                    offset.host = i & 0xffff;
                    offset.path = Number(cF & F.IndexMask);
                    cF &= notIndexMask;
                } else {
                    if ((cF & F.HasUserInfo) !== 0n) {
                        for (; ch(this._string, i) !== AT; i++) { /* the user info */ }
                        i++;
                        offset.host = i & 0xffff;
                    } else {
                        offset.host = i & 0xffff;
                    }

                    i = Number(cF & F.IndexMask);
                    cF &= notIndexMask;
                    if (backslash) cF |= F.SchemeNotCanonical;
                    offset.path = i & 0xffff;
                    let portFound = false;
                    if ((cF & F.HasUnicode) !== 0n) offset.end = this._originalUnicodeString!.length & 0xffff;
                    if (i < offset.end) {
                        const originalString = this.originalString;
                        if (pc(originalString, i) === COLON) {
                            let port = 0;
                            if (++i < offset.end) {
                                port = pc(originalString, i) - 0x30;
                                if (port >= 0 && port <= 9) {
                                    portFound = true;
                                    if (port === 0) cF |= F.PortNotCanonical | F.E_PortNotCanonical;
                                    for (i++; i < offset.end; i++) {
                                        const digit = pc(originalString, i) - 0x30;
                                        if (digit < 0 || digit > 9) break;
                                        port = port * 10 + digit;
                                    }
                                }
                            }

                            if (portFound && this._syntax!.defaultPort !== port) {
                                offset.portValue = port & 0xffff;
                                cF |= F.NotDefaultPort;
                            } else {
                                cF |= F.PortNotCanonical | F.E_PortNotCanonical;
                            }

                            offset.path = i & 0xffff;
                        }
                    }
                }
            }
        }

        cF |= F.MinimalUriInfoSet;
        this._info ??= info;
        if ((this._flags & F.MinimalUriInfoSet) === 0n) {
            this._flags = (this._flags & notIndexMask) | cF;
        }
    }

    /** Uri.CreateHostString */
    private createHostString(): void {
        const info = this._info!;
        const flags: Ref<bigint> = { value: this._flags };
        let text = createHostStringHelper(this._string, info.offset.host, info.offset.path, flags, info.scopeId);
        if (text.length !== 0) {
            if (this.hostType === F.BasicHostType) {
                const idx = { value: 0 };
                let check = this.checkCanonical(text, idx, text.length, NOCHAR);
                if ((check & C.DisplayCanonical) === 0 && (this.notAny(F.ImplicitFile) || (check & C.ReservedFound) !== 0)) {
                    flags.value |= F.HostNotCanonical;
                }

                if (this.inFact(F.ImplicitFile) && (check & (C.EscapedCanonical | C.ReservedFound)) !== 0) {
                    check &= ~C.EscapedCanonical;
                }

                if ((check & (C.EscapedCanonical | C.BackslashInPath)) !== C.EscapedCanonical) {
                    flags.value |= F.E_HostNotCanonical;
                    if (this.notAny(F.UserEscaped)) {
                        text = escapeString(text, !this.isImplicitFile, UnreservedReservedExceptQuestionMarkHash);
                    }
                }
            } else if (this.notAny(F.CanonicalDnsHost)) {
                if (info.scopeId.value !== null) {
                    flags.value |= F.HostNotCanonical | F.E_HostNotCanonical;
                } else {
                    for (let i = 0; i < text.length; i++) {
                        if (info.offset.host + i >= info.offset.end || text.charCodeAt(i) !== ch(this._string, info.offset.host + i)) {
                            flags.value |= F.HostNotCanonical | F.E_HostNotCanonical;
                            break;
                        }
                    }
                }
            }
        }

        info.host = text;
        this._flags |= flags.value;
    }

    /** Uri.InternalGetComponents (GetParts) */
    private getParts(uriParts: number, formatAs: number): string {
        return this.internalGetComponents(uriParts, formatAs);
    }

    private internalGetComponents(components: number, format: number): string {
        if ((components & UriComponents.SerializationInfoString) !== 0 && components !== UriComponents.SerializationInfoString) {
            throw new ArgumentOutOfRangeException('components', messages.notJustSerialization);
        }

        if ((format & ~3) !== 0) throw new ArgumentOutOfRangeException('format');
        if (this.isNotAbsoluteUri) {
            if (components === UriComponents.SerializationInfoString) return this.getRelativeSerializationString(format);
            throw new InvalidOperationException(messages.notAbsolute);
        }

        return this.getComponentsHelper(components, format);
    }

    private getRelativeSerializationString(format: number): string {
        switch (format) {
            case UriFormat.UriEscaped:
                return escapeString(this._string, true, UnreservedReserved);
            case UriFormat.SafeUnescaped: {
                if (this._string.length === 0) return '';
                const dest = new Vsb();
                unescapeString(this._string, 0, this._string.length, dest, NOCHAR, NOCHAR, NOCHAR, M.EscapeUnescape, null, false);
                return dest.toString();
            }
            default:
                throw new ArgumentOutOfRangeException('format');
        }
    }

    /** Uri.GetComponentsHelper */
    private getComponentsHelper(uriComponents: number, uriFormat: number): string {
        if (uriComponents === UriComponents.Scheme) return this._syntax!.schemeName;
        if ((uriComponents & UriComponents.SerializationInfoString) !== 0) uriComponents |= UriComponents.AbsoluteUri;
        this.ensureParseRemaining();
        if ((uriComponents & UriComponents.NormalizedHost) !== 0) uriComponents |= UriComponents.Host;
        if ((uriComponents & UriComponents.Host) !== 0) this.ensureHostString(true);
        if (uriComponents === UriComponents.Port || uriComponents === UriComponents.StrongPort) {
            if ((this._flags & F.NotDefaultPort) !== 0n || (uriComponents === UriComponents.StrongPort && this._syntax!.defaultPort !== -1)) {
                return String(this._info!.offset.portValue);
            }

            return '';
        }

        if ((uriComponents & UriComponents.StrongPort) !== 0) uriComponents |= UriComponents.Port;
        if (uriComponents === UriComponents.Host && (uriFormat === UriFormat.UriEscaped || (this._flags & (F.HostNotCanonical | F.E_HostNotCanonical)) === 0n)) {
            this.ensureHostString(false);
            return this._info!.host!;
        }

        switch (uriFormat) {
            case UriFormat.UriEscaped:
                return this.getEscapedParts(uriComponents);
            case UriFormat.Unescaped:
            case UriFormat.SafeUnescaped:
            case V1ToString:
                return this.getUnescapedParts(uriComponents, uriFormat);
            default:
                throw new ArgumentOutOfRangeException('uriFormat');
        }
    }

    private getEscapedParts(uriParts: number): string {
        let nonCanonical = Number((this._flags & 0x3f80n) >> 6n);
        if (this.inFact(F.SchemeNotCanonical)) nonCanonical |= 1;
        if ((uriParts & UriComponents.Path) !== 0) {
            if (this.inFact(F.ShouldBeCompressed | F.FirstSlashAbsent | F.BackslashInPath)) {
                nonCanonical |= 0x10;
            } else if (this.isDosPath && ch(this._string, this._info!.offset.path + this.securedPathIndex - 1) === PIPE) {
                nonCanonical |= 0x10;
            }
        }

        if (((uriParts & 0xffff) & nonCanonical) === 0) {
            const fromUser = this.getUriPartsFromUserString(uriParts);
            if (fromUser !== null) return fromUser;
        }

        return this.recreateParts(uriParts, nonCanonical, UriFormat.UriEscaped);
    }

    private getUnescapedParts(uriParts: number, formatAs: number): string {
        let nonCanonical = Number(this._flags & 0x7fn);
        if ((uriParts & UriComponents.Path) !== 0) {
            if ((this._flags & (F.ShouldBeCompressed | F.FirstSlashAbsent | F.BackslashInPath)) !== 0n) {
                nonCanonical |= 0x10;
            } else if (this.isDosPath && ch(this._string, this._info!.offset.path + this.securedPathIndex - 1) === PIPE) {
                nonCanonical |= 0x10;
            }
        }

        if (((uriParts & 0xffff) & nonCanonical) === 0) {
            const fromUser = this.getUriPartsFromUserString(uriParts);
            if (fromUser !== null) return fromUser;
        }

        return this.recreateParts(uriParts, nonCanonical, formatAs);
    }

    /** Uri.RecreateParts */
    private recreateParts(parts: number, nonCanonical: number, formatAs: number): string {
        this.ensureHostString(false);
        const str = this._string;
        const info = this._info!;
        const dest = new Vsb();
        if ((parts & UriComponents.Scheme) !== 0) {
            dest.append(this._syntax!.schemeName);
            if (parts !== UriComponents.Scheme) {
                dest.appendChar(COLON);
                if (this.inFact(F.AuthorityFound)) {
                    dest.appendChar(SLASH);
                    dest.appendChar(SLASH);
                }
            }
        }

        if ((parts & UriComponents.UserInfo) !== 0 && this.inFact(F.HasUserInfo)) {
            const userStart = info.offset.user;
            const userEnd = info.offset.host;
            if ((nonCanonical & 2) !== 0) {
                switch (formatAs) {
                    case UriFormat.UriEscaped:
                        if (this.notAny(F.UserEscaped)) {
                            escapeStringInto(str, userStart, userEnd, dest, true, UnreservedReservedExceptQuestionMarkHash);
                        } else {
                            dest.append(str, userStart, userEnd);
                        }
                        break;
                    case UriFormat.SafeUnescaped:
                        unescapeString(str, userStart, userEnd - 1, dest, AT, SLASH, BACKSLASH, this.inFact(F.UserEscaped) ? M.Unescape : M.EscapeUnescape, this._syntax, false);
                        dest.appendChar(AT);
                        break;
                    case UriFormat.Unescaped:
                        unescapeString(str, userStart, userEnd, dest, NOCHAR, NOCHAR, NOCHAR, M.Unescape | M.UnescapeAll, this._syntax, false);
                        break;
                    default:
                        dest.append(str, userStart, userEnd);
                        break;
                }
            } else {
                dest.append(str, userStart, userEnd);
            }

            if (parts === UriComponents.UserInfo) dest.length--;
        }

        if ((parts & UriComponents.Host) !== 0) {
            const text = info.host!;
            if (text.length !== 0) {
                const unescapeMode = formatAs !== UriFormat.UriEscaped && this.hostType === F.BasicHostType && (nonCanonical & 4) !== 0
                    ? (formatAs === UriFormat.Unescaped ? M.Unescape | M.UnescapeAll : this.inFact(F.UserEscaped) ? M.Unescape : M.EscapeUnescape)
                    : M.CopyOnly;
                // NormalizedHost (IdnMapping) is not reached by the members ported here.
                if ((parts & UriComponents.NormalizedHost) !== 0) throw new Error('UriComponents.NormalizedHost is not ported.');
                unescapeString(text, 0, text.length, dest, SLASH, QUESTION, HASH, unescapeMode, this._syntax, false);
                if ((parts & UriComponents.SerializationInfoString) !== 0 && this.hostType === F.IPv6HostType && info.scopeId.value !== null) {
                    dest.length--;
                    dest.append(info.scopeId.value);
                    dest.appendChar(RBRACKET);
                }
            }
        }

        if ((parts & UriComponents.Port) !== 0 && (this.inFact(F.NotDefaultPort) || ((parts & UriComponents.StrongPort) !== 0 && this._syntax!.defaultPort !== -1))) {
            dest.appendChar(COLON);
            dest.append(String(info.offset.portValue));
        }

        if ((parts & UriComponents.Path) !== 0) {
            this.getCanonicalPath(dest, formatAs);
            if (parts === UriComponents.Path) {
                const start = this.inFact(F.AuthorityFound) && dest.length !== 0 && dest.chars[0] === SLASH ? 1 : 0;
                return dest.toString(start);
            }
        }

        if ((parts & UriComponents.Query) !== 0 && info.offset.query < info.offset.fragment) {
            const queryStart = info.offset.query + 1;
            if (parts !== UriComponents.Query) dest.appendChar(QUESTION);
            let mode: number = M.CopyOnly;
            let done = false;
            if ((nonCanonical & 0x20) !== 0) {
                if (formatAs === UriFormat.UriEscaped) {
                    if (this.notAny(F.UserEscaped)) {
                        escapeStringInto(str, queryStart, info.offset.fragment, dest, true, UnreservedReservedExceptHash);
                        done = true;
                    }
                } else {
                    mode = formatAs === V1ToString ? (this.inFact(F.UserEscaped) ? 2 : 3) | 4
                        : formatAs === UriFormat.Unescaped ? M.Unescape | M.UnescapeAll
                            : this.inFact(F.UserEscaped) ? M.Unescape : M.EscapeUnescape;
                }
            }

            if (!done) unescapeString(str, queryStart, info.offset.fragment, dest, HASH, NOCHAR, NOCHAR, mode, this._syntax, true);
        }

        if ((parts & UriComponents.Fragment) !== 0 && info.offset.fragment < info.offset.end) {
            const fragmentStart = info.offset.fragment + 1;
            if (parts !== UriComponents.Fragment) dest.appendChar(HASH);
            let mode: number = M.CopyOnly;
            if ((nonCanonical & 0x40) !== 0) {
                if (formatAs === UriFormat.UriEscaped) {
                    if (this.notAny(F.UserEscaped)) {
                        escapeStringInto(str, fragmentStart, info.offset.end, dest, true, UnreservedReserved);
                        return dest.toString();
                    }
                } else {
                    mode = formatAs === V1ToString ? (this.inFact(F.UserEscaped) ? 2 : 3) | 4
                        : formatAs === UriFormat.Unescaped ? M.Unescape | M.UnescapeAll
                            : this.inFact(F.UserEscaped) ? M.Unescape : M.EscapeUnescape;
                }
            }

            unescapeString(str, fragmentStart, info.offset.end, dest, HASH, NOCHAR, NOCHAR, mode, this._syntax, false);
        }

        return dest.toString();
    }

    /** Uri.GetUriPartsFromUserString */
    private getUriPartsFromUserString(uriParts: number): string | null {
        const s = this._string;
        const o = this._info!.offset;
        switch (uriParts & ~UriComponents.KeepDelimiter) {
            case UriComponents.SchemeAndServer:
                if (!this.inFact(F.HasUserInfo)) return s.substring(o.scheme, o.path);
                return s.substring(o.scheme, o.user) + s.substring(o.host, o.path);
            case UriComponents.HostAndPort:
                if (this.inFact(F.HasUserInfo)) {
                    if (this.inFact(F.NotDefaultPort) || this._syntax!.defaultPort === -1) return s.substring(o.host, o.path);
                    return s.substring(o.host, o.path) + ':' + String(o.portValue);
                }

                return this.strongAuthorityFromUserString();
            case UriComponents.AbsoluteUri:
                if (o.scheme === 0 && o.end === s.length) return s;
                return s.substring(o.scheme, o.end);
            case UriComponents.HttpRequestUrl:
                if (this.inFact(F.HasUserInfo)) return s.substring(o.scheme, o.user) + s.substring(o.host, o.fragment);
                if (o.scheme === 0 && o.fragment === s.length) return s;
                return s.substring(o.scheme, o.fragment);
            case UriComponents.SchemeAndServer | UriComponents.UserInfo:
                return s.substring(o.scheme, o.path);
            case UriComponents.HttpRequestUrl | UriComponents.UserInfo:
                if (o.scheme === 0 && o.fragment === s.length) return s;
                return s.substring(o.scheme, o.fragment);
            case UriComponents.Scheme:
                if (uriParts !== UriComponents.Scheme) return s.substring(o.scheme, o.user);
                return this._syntax!.schemeName;
            case UriComponents.Host: {
                let end = o.path;
                if (this.inFact(F.PortNotCanonical | F.NotDefaultPort)) {
                    while (ch(s, --end) !== COLON) { /* back to the port */ }
                }

                return end - o.host !== 0 ? s.substring(o.host, end) : '';
            }
            case UriComponents.Path: {
                const start = uriParts !== UriComponents.Path || !this.inFact(F.AuthorityFound) || o.end <= o.path || ch(s, o.path) !== SLASH ? o.path : o.path + 1;
                return start >= o.query ? '' : s.substring(start, o.query);
            }
            case UriComponents.Query: {
                const start = uriParts !== UriComponents.Query ? o.query : o.query + 1;
                return start >= o.fragment ? '' : s.substring(start, o.fragment);
            }
            case UriComponents.Fragment: {
                const start = uriParts !== UriComponents.Fragment ? o.fragment : o.fragment + 1;
                return start >= o.end ? '' : s.substring(start, o.end);
            }
            case UriComponents.UserInfo | UriComponents.Host | UriComponents.Port:
                return o.path - o.user !== 0 ? s.substring(o.user, o.path) : '';
            case UriComponents.StrongAuthority:
                return this.strongAuthorityFromUserString();
            case UriComponents.PathAndQuery:
                return s.substring(o.path, o.fragment);
            case UriComponents.HttpRequestUrl | UriComponents.Fragment:
                if (this.inFact(F.HasUserInfo)) return s.substring(o.scheme, o.user) + s.substring(o.host, o.end);
                if (o.scheme === 0 && o.end === s.length) return s;
                return s.substring(o.scheme, o.end);
            case UriComponents.PathAndQuery | UriComponents.Fragment:
                return s.substring(o.path, o.end);
            case UriComponents.UserInfo: {
                if (this.notAny(F.HasUserInfo)) return '';
                const end = uriParts !== UriComponents.UserInfo ? o.host : o.host - 1;
                return o.user >= end ? '' : s.substring(o.user, end);
            }
            default:
                return null;
        }
    }

    private strongAuthorityFromUserString(): string {
        const s = this._string;
        const o = this._info!.offset;
        if (!this.inFact(F.NotDefaultPort) && this._syntax!.defaultPort !== -1) return s.substring(o.user, o.path) + ':' + String(o.portValue);
        return o.path - o.user !== 0 ? s.substring(o.user, o.path) : '';
    }

    /** Uri.ParseRemaining */
    private parseRemaining(): void {
        const info = this.ensureUriInfo();
        let flags = F.Zero;
        if (!this.userDrivenParsing) {
            const hasUnicode = this.inFact(F.HasUnicode);
            let idx = info.offset.scheme;
            const lengthRef = { value: this._string.length };
            let check: number = C.None;
            const syntaxFlags = this._syntax!.flags;
            const ptr = this._string;
            getLengthWithoutTrailingSpaces(this._string, lengthRef, idx);
            if (this.isImplicitFile) {
                flags |= F.SchemeNotCanonical;
            } else {
                const schemeName = this._syntax!.schemeName;
                let i: number;
                for (i = 0; i < schemeName.length; i++) {
                    if (schemeName.charCodeAt(i) !== pc(ptr, idx + i)) flags |= F.SchemeNotCanonical;
                }

                if ((this._flags & F.AuthorityFound) !== 0n && (idx + i + 3 >= lengthRef.value || pc(ptr, idx + i + 1) !== SLASH || pc(ptr, idx + i + 2) !== SLASH)) {
                    flags |= F.SchemeNotCanonical;
                }
            }

            if ((this._flags & F.HasUserInfo) !== 0n) {
                const userIdx = { value: info.offset.user };
                check = this.checkCanonical(ptr, userIdx, info.offset.host, AT);
                if ((check & C.DisplayCanonical) === 0) flags |= F.UserNotCanonical;
                if ((check & (C.EscapedCanonical | C.BackslashInPath)) !== C.EscapedCanonical) flags |= F.E_UserNotCanonical;
                if (this.iriParsing && (check & (C.EscapedCanonical | C.DisplayCanonical | C.BackslashInPath | C.NotIriCanonical | C.FoundNonAscii)) === (C.DisplayCanonical | C.FoundNonAscii)) {
                    flags |= F.UserIriCanonical;
                }
            }

            const position = { value: info.offset.path };
            let num = info.offset.path;
            if (hasUnicode) {
                if (this.isFile && !this.isUncPath) {
                    this._string = this.isImplicitFile ? '' : this._syntax!.schemeName + '://';
                }

                info.offset.path = this._string.length & 0xffff;
                position.value = info.offset.path;
            }

            if (this.disablePathAndQueryCanonicalization) {
                if (hasUnicode) this._string = this._string + this._originalUnicodeString!.substring(num);
                const text = this._string;
                let query: number;
                if (this.isImplicitFile || (syntaxFlags & S.MayHaveQuery) === 0) {
                    query = text.length;
                } else {
                    query = text.indexOf('?');
                    if (query === -1) query = text.length;
                }

                info.offset.query = query & 0xffff;
                info.offset.fragment = text.length & 0xffff;
                info.offset.end = text.length & 0xffff;
            } else {
                const original = this._originalUnicodeString ?? '';
                if (hasUnicode) {
                    const start = num;
                    if (this.isImplicitFile || (syntaxFlags & (S.MayHaveQuery | S.MayHaveFragment)) === 0) {
                        num = original.length;
                    } else {
                        let found = -1;
                        for (let k = num; k < original.length; k++) {
                            const c = original.charCodeAt(k);
                            if (this._syntax!.inFact(S.MayHaveQuery) ? (c === QUESTION || (this._syntax!.inFact(S.MayHaveFragment) && c === HASH)) : c === HASH) {
                                found = k - num;
                                break;
                            }
                        }

                        num = found === -1 ? original.length : found + num;
                    }

                    this._string += escapeUnescapeIri(original, start, num, UriComponents.Path);
                    if (this._string.length > 65535) throw getException(ParsingError.SizeLimit)!;
                    lengthRef.value = this._string.length;
                    if (this._string === original) getLengthWithoutTrailingSpaces(this._string, lengthRef, position.value);
                }

                const ptr2 = this._string;
                if (!this.isImplicitFile && (syntaxFlags & (S.MayHaveQuery | S.MayHaveFragment)) !== 0) {
                    const delim = (syntaxFlags & S.MayHaveQuery) !== 0 ? QUESTION : this._syntax!.inFact(S.MayHaveFragment) ? HASH : 0xfffe;
                    check = this.checkCanonical(ptr2, position, lengthRef.value, delim);
                } else {
                    check = this.checkCanonical(ptr2, position, lengthRef.value, NOCHAR);
                }

                if ((this._flags & F.AuthorityFound) !== 0n && (syntaxFlags & S.PathIsRooted) !== 0
                    && (info.offset.path === lengthRef.value || (pc(ptr2, info.offset.path) !== SLASH && pc(ptr2, info.offset.path) !== BACKSLASH))) {
                    flags |= F.FirstSlashAbsent;
                }

                let pathNotCanonical = false;
                if (this.isDosPath || ((this._flags & F.AuthorityFound) !== 0n
                    && ((syntaxFlags & (S.ConvertPathSlashes | S.CompressPath)) !== 0 || this._syntax!.inFact(S.UnEscapeDotsAndSlashes)))) {
                    if ((check & C.DotSlashEscaped) !== 0 && this._syntax!.inFact(S.UnEscapeDotsAndSlashes)) {
                        flags |= F.PathNotCanonical | F.E_PathNotCanonical;
                        pathNotCanonical = true;
                    }

                    if ((syntaxFlags & S.ConvertPathSlashes) !== 0 && (check & C.BackslashInPath) !== 0) {
                        flags |= F.PathNotCanonical | F.E_PathNotCanonical;
                        pathNotCanonical = true;
                    }

                    if ((syntaxFlags & S.CompressPath) !== 0 && ((flags & F.E_PathNotCanonical) !== 0n || (check & C.DotSlashAttn) !== 0)) {
                        flags |= F.ShouldBeCompressed;
                    }

                    if ((check & C.BackslashInPath) !== 0) flags |= F.BackslashInPath;
                } else if ((check & C.BackslashInPath) !== 0) {
                    flags |= F.E_PathNotCanonical;
                    pathNotCanonical = true;
                }

                if ((check & C.DisplayCanonical) === 0
                    && ((this._flags & F.ImplicitFile) === 0n || (this._flags & F.UserEscaped) !== 0n || (check & C.ReservedFound) !== 0)) {
                    flags |= F.PathNotCanonical;
                    pathNotCanonical = true;
                }

                if ((this._flags & F.ImplicitFile) !== 0n && (check & (C.EscapedCanonical | C.ReservedFound)) !== 0) check &= ~C.EscapedCanonical;
                if ((check & C.EscapedCanonical) === 0) flags |= F.E_PathNotCanonical;
                if (this.iriParsing && !pathNotCanonical
                    && (check & (C.EscapedCanonical | C.DisplayCanonical | C.NotIriCanonical | C.FoundNonAscii)) === (C.DisplayCanonical | C.FoundNonAscii)) {
                    flags |= F.PathIriCanonical;
                }

                if (hasUnicode) {
                    const start = num;
                    if (num < original.length && original.charCodeAt(num) === QUESTION) {
                        if ((syntaxFlags & S.MayHaveFragment) !== 0) {
                            num++;
                            const hash = original.indexOf('#', num);
                            num = hash === -1 ? original.length : hash;
                        } else {
                            num = original.length;
                        }

                        this._string += escapeUnescapeIri(original, start, num, UriComponents.Query);
                        if (this._string.length > 65535) throw getException(ParsingError.SizeLimit)!;
                        lengthRef.value = this._string.length;
                        if (this._string === original) getLengthWithoutTrailingSpaces(this._string, lengthRef, position.value);
                    }
                }

                info.offset.query = position.value & 0xffff;
                const ptr3 = this._string;
                if (position.value < lengthRef.value && pc(ptr3, position.value) === QUESTION) {
                    position.value++;
                    check = this.checkCanonical(ptr3, position, lengthRef.value, (syntaxFlags & S.MayHaveFragment) !== 0 ? HASH : 0xfffe);
                    if ((check & C.DisplayCanonical) === 0) flags |= F.QueryNotCanonical;
                    if ((check & (C.EscapedCanonical | C.BackslashInPath)) !== C.EscapedCanonical) flags |= F.E_QueryNotCanonical;
                    if (this.iriParsing && (check & (C.EscapedCanonical | C.DisplayCanonical | C.BackslashInPath | C.NotIriCanonical | C.FoundNonAscii)) === (C.DisplayCanonical | C.FoundNonAscii)) {
                        flags |= F.QueryIriCanonical;
                    }
                }

                if (hasUnicode) {
                    const start = num;
                    if (num < original.length && original.charCodeAt(num) === HASH) {
                        num = original.length;
                        this._string += escapeUnescapeIri(original, start, num, UriComponents.Fragment);
                        if (this._string.length > 65535) throw getException(ParsingError.SizeLimit)!;
                        lengthRef.value = this._string.length;
                        getLengthWithoutTrailingSpaces(this._string, lengthRef, position.value);
                    }
                }

                info.offset.fragment = position.value & 0xffff;
                const ptr4 = this._string;
                if (position.value < lengthRef.value && pc(ptr4, position.value) === HASH) {
                    position.value++;
                    check = this.checkCanonical(ptr4, position, lengthRef.value, 0xfffe);
                    if ((check & C.DisplayCanonical) === 0) flags |= F.FragmentNotCanonical;
                    if ((check & (C.EscapedCanonical | C.BackslashInPath)) !== C.EscapedCanonical) flags |= F.E_FragmentNotCanonical;
                    if (this.iriParsing && (check & (C.EscapedCanonical | C.DisplayCanonical | C.BackslashInPath | C.NotIriCanonical | C.FoundNonAscii)) === (C.DisplayCanonical | C.FoundNonAscii)) {
                        flags |= F.FragmentIriCanonical;
                    }
                }

                info.offset.end = position.value & 0xffff;
            }
        }

        flags |= F.AllUriInfoSet;
        this._flags |= flags;
    }

    /** Uri.CheckAuthorityHelper: the end of the authority (of the host, when a port follows). */
    private checkAuthorityHelper(pString: string, idx: number, length: number, err: Ref<ParsingError>, flags: Ref<bigint>, syntax: UriParser,
        newHost: Ref<string | null>): number {
        let i = length;
        const start = idx;
        let j = idx;
        newHost.value = null;
        const hasUnicode = (flags.value & F.HasUnicode) !== 0n;
        const syntaxFlags = syntax.flags;
        if (hasUnicode) newHost.value = this._originalUnicodeString!.substring(0, start);
        let c = 0;
        if (idx === length || (c = pc(pString, idx)) === SLASH || (c === BACKSLASH && syntax.inFact(S.FileLikeUri)) || c === HASH || c === QUESTION) {
            if (syntax.inFact(S.AllowEmptyHost)) {
                flags.value &= ~F.UncPath;
                if ((flags.value & F.ImplicitFile) !== 0n) {
                    err.value = ParsingError.BadHostName;
                } else {
                    flags.value |= F.BasicHostType;
                }
            } else {
                err.value = ParsingError.BadHostName;
            }

            return idx;
        }

        if ((syntaxFlags & S.MayHaveUserInfo) !== 0) {
            for (; j < i; j++) {
                const cj = pc(pString, j);
                if (j === i - 1 || cj === QUESTION || cj === HASH || cj === BACKSLASH || cj === SLASH) {
                    j = idx;
                    break;
                }

                if (cj !== AT) continue;
                flags.value |= F.HasUserInfo;
                if (hasUnicode) {
                    newHost.value += escapeUnescapeIri(pString, start, j + 1, UriComponents.UserInfo);
                    if (newHost.value!.length > 65535) {
                        err.value = ParsingError.SizeLimit;
                        return idx;
                    }
                }

                j++;
                c = pc(pString, j);
                break;
            }
        }

        const notImplicitFile = (flags.value & F.ImplicitFile) === 0n;
        const end: Ref<number> = { value: i };
        if (c === LBRACKET && syntax.inFact(S.AllowIPv6Host) && ipv6IsValid(pString, j + 1, end)) {
            i = end.value;
            flags.value |= F.IPv6HostType;
            if (hasUnicode) newHost.value += pString.substring(j, i);
        } else if (isAsciiDigit(c) && syntax.inFact(S.AllowIPv4Host)
            && ipv4IsValid(pString, j, end, false, notImplicitFile, syntax.inFact(S.V1_UnknownUri))) {
            i = end.value;
            flags.value |= F.IPv4HostType;
            if (hasUnicode) newHost.value += pString.substring(j, i);
        } else {
            // IPv4AddressHelper.ParseNonCanonical moves the end even when it fails late.
            i = end.value;
            let dns: { valid: boolean; length: number };
            if ((syntaxFlags & S.AllowDnsHost) !== 0 && !iriParsingStatic(syntax)
                && (dns = domainIsValid(pString.substring(j, i), false, notImplicitFile)).valid) {
                i = j + dns.length;
                flags.value |= F.DnsHostType;
                if (!/[A-Z]/.test(pString.substring(j, j + dns.length))) flags.value |= F.CanonicalDnsHost;
            } else if ((syntaxFlags & S.AllowDnsHost) !== 0 && (hasUnicode || syntax.inFact(S.AllowIdn))
                && (dns = domainIsValid(pString.substring(j, i), true, notImplicitFile)).valid) {
                i = j + dns.length;
                flags.value |= F.DnsHostType;
                // CheckAuthorityHelperHandleDnsIri; Normalize(FormC) is the identity under invariant globalization.
                if (hasUnicode) newHost.value += stripBidiControlCharacters(pString.substring(j, i));
            } else {
                const uncEnd: Ref<number> = { value: i };
                if ((syntaxFlags & S.AllowUncHost) !== 0 && uncIsValid(pString, j, uncEnd, notImplicitFile) && uncEnd.value - j <= 256) {
                    i = uncEnd.value;
                    flags.value |= F.UncHostType;
                    if (hasUnicode) newHost.value += pString.substring(j, i);
                }
            }
        }

        if (i < length && pc(pString, i) === BACKSLASH && (flags.value & F.HostTypeMask) !== 0n && !syntax.inFact(S.FileLikeUri)) {
            if (syntax.inFact(S.V1_UnknownUri)) {
                err.value = ParsingError.BadHostName;
                flags.value |= F.HostTypeMask;
                return i;
            }

            flags.value &= ~F.HostTypeMask;
        } else if (i < length && pc(pString, i) === COLON) {
            if (syntax.inFact(S.MayHavePort)) {
                let port = 0;
                const portStart = i;
                for (idx = i + 1; idx < length; idx++) {
                    const digit = pc(pString, idx) - 0x30;
                    if (digit >= 0 && digit <= 9) {
                        if ((port = port * 10 + digit) <= 0xffff) continue;
                        break;
                    }

                    if (digit === -13 || digit === -1 || digit === 15) break;
                    if (syntax.inFact(S.AllowAnyOtherHost) && syntax.notAny(S.V1_UnknownUri)) {
                        flags.value &= ~F.HostTypeMask;
                        break;
                    }

                    err.value = ParsingError.BadPort;
                    return idx;
                }

                if (port > 0xffff) {
                    if (!syntax.inFact(S.AllowAnyOtherHost)) {
                        err.value = ParsingError.BadPort;
                        return idx;
                    }

                    flags.value &= ~F.HostTypeMask;
                }

                if (hasUnicode) newHost.value += pString.substring(portStart, idx);
            } else {
                flags.value &= ~F.HostTypeMask;
            }
        }

        if ((flags.value & F.HostTypeMask) === 0n) {
            flags.value &= ~F.HasUserInfo;
            if (syntax.inFact(S.AllowAnyOtherHost)) {
                flags.value |= F.BasicHostType;
                for (i = idx; i < length && pc(pString, i) !== SLASH && pc(pString, i) !== QUESTION && pc(pString, i) !== HASH; i++) { /* the host */ }
                if (hasUnicode) newHost.value += pString.substring(start, i);
            } else if (syntax.inFact(S.V1_UnknownUri)) {
                let dotFound = false;
                const basicStart = idx;
                for (i = idx; i < length && (!dotFound || (pc(pString, i) !== SLASH && pc(pString, i) !== QUESTION && pc(pString, i) !== HASH)); i++) {
                    if (i < idx + 2 && pc(pString, i) === DOT) {
                        dotFound = true;
                        continue;
                    }

                    err.value = ParsingError.BadHostName;
                    flags.value |= F.HostTypeMask;
                    return idx;
                }

                flags.value |= F.BasicHostType;
                if (hasUnicode) newHost.value += pString.substring(basicStart, i);
            } else if (syntax.inFact(S.MustHaveAuthority) || syntax.inFact(S.MailToLikeUri)) {
                err.value = ParsingError.BadHostName;
                flags.value |= F.HostTypeMask;
                return idx;
            }
        }

        return i;
    }

    /** Uri.CheckCanonical over str[idx, end): what the part needs; `idx` moves to where it stopped. */
    private checkCanonical(str: string, idx: Ref<number>, end: number, delim: number): number {
        let check: number = C.None;
        let needsEscaping = false;
        let foundEscaping = false;
        const iriParsing = this.iriParsing;
        let i: number;
        for (i = idx.value; i < end; i++) {
            let c = pc(str, i);
            if (c <= 0x1f || (c >= 0x7f && c <= 0x9f)) {
                needsEscaping = true;
                foundEscaping = true;
                check |= C.ReservedFound;
                continue;
            }

            if (c > 0x7e) {
                if (iriParsing) {
                    let ok = false;
                    check |= C.FoundNonAscii;
                    if (isHighSurrogate(c)) {
                        if (i + 1 < end) ok = checkIriUnicodeRangePair(c, pc(str, i + 1), true).ok;
                    } else {
                        ok = checkIriUnicodeRangeChar(c, true);
                    }

                    if (!ok) check |= C.NotIriCanonical;
                }

                needsEscaping = true;
                continue;
            }

            if (c === delim || (delim === QUESTION && c === HASH && this._syntax !== null && this._syntax.inFact(S.MayHaveFragment))) break;
            if (c === QUESTION) {
                if (this.isImplicitFile || (this._syntax !== null && !this._syntax.inFact(S.MayHaveQuery) && delim !== 0xfffe)) {
                    check |= C.ReservedFound;
                    foundEscaping = true;
                    needsEscaping = true;
                }

                continue;
            }

            if (c === HASH) {
                needsEscaping = true;
                if (this.isImplicitFile || (this._syntax !== null && !this._syntax.inFact(S.MayHaveFragment))) {
                    check |= C.ReservedFound;
                    foundEscaping = true;
                }

                continue;
            }

            if (c === SLASH || c === BACKSLASH) {
                if ((check & C.BackslashInPath) === 0 && c === BACKSLASH) check |= C.BackslashInPath;
                if ((check & C.DotSlashAttn) === 0 && i + 1 !== end && (pc(str, i + 1) === SLASH || pc(str, i + 1) === BACKSLASH)) check |= C.DotSlashAttn;
                continue;
            }

            if (c === DOT) {
                if (((check & C.DotSlashAttn) === 0 && i + 1 === end) || pc(str, i + 1) === DOT || pc(str, i + 1) === SLASH || pc(str, i + 1) === BACKSLASH
                    || pc(str, i + 1) === QUESTION || pc(str, i + 1) === HASH) {
                    check |= C.DotSlashAttn;
                }

                continue;
            }

            if ((c > 0x22 || c === 0x21) && (c < 0x5b || c > 0x5e)) {
                if (c === 0x3c || c === 0x3e || c === 0x60) {
                    // falls to the end of the loop body
                } else if (c === 0x7b || c === 0x7c || c === 0x7d) {
                    needsEscaping = true;
                    continue;
                } else {
                    if (c !== PERCENT) continue;
                    foundEscaping = true;
                    if (i + 2 < end && (c = decodeHexChars(pc(str, i + 1), pc(str, i + 2))) !== NOCHAR) {
                        if (c === DOT || c === SLASH || c === BACKSLASH) check |= C.DotSlashEscaped;
                        i += 2;
                    } else {
                        needsEscaping = true;
                    }

                    continue;
                }
            }

            needsEscaping = true;
            if ((this._flags & F.HasUnicode) !== 0n) check |= C.NotIriCanonical;
        }

        if (foundEscaping) {
            if (!needsEscaping) check |= C.EscapedCanonical;
        } else {
            check |= C.DisplayCanonical;
            if (!needsEscaping) check |= C.EscapedCanonical;
        }

        idx.value = i;
        return check;
    }

    /** Uri.GetCanonicalPath */
    private getCanonicalPath(dest: Vsb, formatAs: number): void {
        const info = this._info!;
        if (this.inFact(F.FirstSlashAbsent)) dest.appendChar(SLASH);
        if (info.offset.path === info.offset.query) return;
        let start = dest.length;
        const securedPathIndex = this.securedPathIndex;
        const unescapeDotsAndSlashes = () => {
            dest.length = unescapeOnly(dest.chars, start, dest.length, DOT, SLASH, this._syntax!.inFact(S.ConvertPathSlashes) ? BACKSLASH : NOCHAR);
        };
        if (formatAs === UriFormat.UriEscaped) {
            if (this.inFact(F.ShouldBeCompressed)) {
                dest.append(this._string, info.offset.path, info.offset.query);
                if (this._syntax!.inFact(S.UnEscapeDotsAndSlashes) && this.inFact(F.PathNotCanonical) && !this.isImplicitFile) unescapeDotsAndSlashes();
            } else if (this.inFact(F.E_PathNotCanonical) && this.notAny(F.UserEscaped)) {
                let text = this._string;
                if (securedPathIndex !== 0 && ch(text, securedPathIndex + info.offset.path - 1) === PIPE) {
                    const at = securedPathIndex + info.offset.path - 1;
                    text = text.substring(0, at) + ':' + text.substring(at + 1);
                }

                escapeStringInto(text, info.offset.path, info.offset.query, dest, !this.isImplicitFile, UnreservedReservedExceptQuestionMarkHash);
            } else {
                dest.append(this._string, info.offset.path, info.offset.query);
            }

            if (this.inFact(F.BackslashInPath) && this._syntax!.notAny(S.ConvertPathSlashes) && this._syntax!.inFact(S.FileLikeUri) && !this.isImplicitFile) {
                const copy = dest.toString(start);
                dest.length = start;
                escapeStringInto(copy, 0, copy.length, dest, true, UnreservedReserved);
                start = dest.length;
            }
        } else {
            dest.append(this._string, info.offset.path, info.offset.query);
            if (this.inFact(F.ShouldBeCompressed) && this._syntax!.inFact(S.UnEscapeDotsAndSlashes) && this.inFact(F.PathNotCanonical) && !this.isImplicitFile) {
                unescapeDotsAndSlashes();
            }
        }

        const secured = start + securedPathIndex;
        if (securedPathIndex !== 0 && dest.chars[secured - 1] === PIPE) dest.chars[secured - 1] = COLON;
        if (this.inFact(F.ShouldBeCompressed) && dest.length - secured > 0) {
            dest.length = secured + compress(dest.chars, secured, dest.length - secured, this._syntax!);
            if (dest.chars[start] === BACKSLASH) dest.chars[start] = SLASH;
            if (formatAs === UriFormat.UriEscaped && this.notAny(F.UserEscaped) && this.inFact(F.E_PathNotCanonical)) {
                const copy = dest.toString(start);
                dest.length = start;
                escapeStringInto(copy, 0, copy.length, dest, !this.isImplicitFile, UnreservedReservedExceptQuestionMarkHash);
                start = dest.length;
            }
        }

        if (formatAs === UriFormat.UriEscaped || !this.inFact(F.PathNotCanonical)) return;
        let unescapeMode: number;
        switch (formatAs) {
            case V1ToString:
                unescapeMode = (this.inFact(F.UserEscaped) ? 2 : 3) | 4;
                if (this.isImplicitFile) unescapeMode &= ~M.Unescape;
                break;
            case UriFormat.Unescaped:
                unescapeMode = !this.isImplicitFile ? M.Unescape | M.UnescapeAll : M.CopyOnly;
                break;
            default:
                unescapeMode = this.inFact(F.UserEscaped) ? M.Unescape : M.EscapeUnescape;
                if (this.isImplicitFile) unescapeMode &= ~M.Unescape;
                break;
        }

        if (unescapeMode !== M.CopyOnly) {
            const copy = dest.toString(start);
            dest.length = start;
            unescapeString(copy, 0, copy.length, dest, QUESTION, HASH, NOCHAR, unescapeMode, this._syntax, false);
        }
    }

    // new Uri(Uri, string) and what it uses.

    /** Uri.CreateUri */
    private createUri(baseUri: Uri, relativeUri: string | null, dontEscape: boolean): void {
        this.createThis(relativeUri, dontEscape, UriKind.RelativeOrAbsolute);
        // A built-in parser is always simple.
        const newUriString: Ref<string | null> = { value: relativeUri };
        const userEscaped: Ref<boolean> = { value: dontEscape };
        const uri = Uri.resolveHelper(baseUri, this, newUriString, userEscaped);
        if (uri !== null) {
            if (this !== uri) this.createThisFromUri(uri);
            return;
        }

        this._flags = F.Zero;
        this._info = null;
        this._syntax = null;
        this._originalUnicodeString = null;
        this.createThis(newUriString.value, userEscaped.value, UriKind.Absolute);
    }

    /** Uri.CreateThisFromUri */
    private createThisFromUri(otherUri: Uri): void {
        this._info = null;
        this._flags = otherUri._flags;
        if (this.inFact(F.MinimalUriInfoSet)) {
            this._flags &= ~(F.IndexMask | F.MinimalUriInfoSet | F.AllUriInfoSet);
            let num = otherUri._info!.offset.path;
            if (this.inFact(F.NotDefaultPort)) {
                while (ch(otherUri._string, num) !== COLON && num > otherUri._info!.offset.host) num--;
                if (ch(otherUri._string, num) !== COLON) num = otherUri._info!.offset.path;
            }

            this._flags |= BigInt(num);
        }

        this._syntax = otherUri._syntax;
        this._string = otherUri._string;
        this._originalUnicodeString = otherUri._originalUnicodeString;
    }

    /** Uri.ResolveHelper */
    private static resolveHelper(baseUri: Uri, relativeUri: Uri | null, newUriString: Ref<string | null>, userEscaped: Ref<boolean>): Uri | null {
        let text: string;
        if (relativeUri !== null) {
            if (relativeUri.isAbsoluteUri) return relativeUri;
            text = relativeUri.originalString;
            userEscaped.value = relativeUri.userEscaped;
        } else {
            text = '';
        }

        if (text.length > 0 && (isLWS(text.charCodeAt(0)) || isLWS(text.charCodeAt(text.length - 1)))) {
            text = text.replace(/^[ \n\r\t]+|[ \n\r\t]+$/g, '');
        }

        if (text.length === 0) {
            newUriString.value = baseUri.getParts(UriComponents.AbsoluteUri, baseUri.userEscaped ? UriFormat.UriEscaped : UriFormat.SafeUnescaped);
            return null;
        }

        if (text.charCodeAt(0) === HASH && !baseUri.isImplicitFile && baseUri._syntax!.inFact(S.MayHaveFragment)) {
            newUriString.value = baseUri.getParts(UriComponents.HttpRequestUrl | UriComponents.UserInfo, UriFormat.UriEscaped) + text;
            return null;
        }

        if (text.charCodeAt(0) === QUESTION && !baseUri.isImplicitFile && baseUri._syntax!.inFact(S.MayHaveQuery)) {
            newUriString.value = baseUri.getParts(UriComponents.SchemeAndServer | UriComponents.UserInfo | UriComponents.Path, UriFormat.UriEscaped) + text;
            return null;
        }

        if (text.length >= 3 && (text.charCodeAt(1) === COLON || text.charCodeAt(1) === PIPE) && isAsciiLetter(text.charCodeAt(0))
            && (text.charCodeAt(2) === BACKSLASH || text.charCodeAt(2) === SLASH)) {
            if (baseUri.isImplicitFile) {
                newUriString.value = text;
                return null;
            }

            if (baseUri._syntax!.inFact(S.AllowDOSPath)) {
                const separator = !baseUri.inFact(F.AuthorityFound)
                    ? (baseUri._syntax!.inFact(S.PathIsRooted) ? ':/' : ':')
                    : (baseUri._syntax!.inFact(S.PathIsRooted) ? ':///' : '://');
                newUriString.value = baseUri.scheme + separator + text;
                return null;
            }
        }

        Uri.getCombinedString(baseUri, text, userEscaped.value, newUriString);
        // `(object)newUriString == baseUri._string`, a reference comparison: true when the combination gave the base's
        // OriginalString and that is its _string (no Unicode was rewritten). Another string equal to it would be parsed
        // anew into the same Uri, so comparing the values gives the same Uri.
        if (baseUri._originalUnicodeString === null && newUriString.value === baseUri._string) return baseUri;
        return null;
    }

    /** Uri.GetCombinedString */
    private static getCombinedString(baseUri: Uri, relativeStr: string, dontEscape: boolean, result: Ref<string | null>): void {
        for (let i = 0; i < relativeStr.length; i++) {
            const c = relativeStr.charCodeAt(i);
            if (c === SLASH || c === BACKSLASH || c === QUESTION || c === HASH) break;
            if (c === COLON) {
                if (i < 2) break;
                const error: Ref<ParsingError> = { value: ParsingError.None };
                const parser = checkSchemeSyntax(relativeStr.substring(0, i), error);
                if (error.value !== ParsingError.None) break;
                if (baseUri._syntax === parser) {
                    relativeStr = i + 1 >= relativeStr.length ? '' : relativeStr.substring(i + 1);
                    break;
                }

                result.value = relativeStr;
                return;
            }
        }

        if (relativeStr.length === 0) {
            result.value = baseUri.originalString;
        } else {
            result.value = Uri.combineUri(baseUri, relativeStr, dontEscape ? UriFormat.UriEscaped : UriFormat.SafeUnescaped);
        }
    }

    /** Uri.CombineUri */
    private static combineUri(basePart: Uri, relativePart: string, uriFormat: number): string {
        let c = relativePart.charCodeAt(0);
        if (basePart.isDosPath && (c === SLASH || c === BACKSLASH) && (relativePart.length === 1 || (relativePart.charCodeAt(1) !== SLASH && relativePart.charCodeAt(1) !== BACKSLASH))) {
            let num = basePart.originalString.indexOf(':');
            if (basePart.isImplicitFile) return basePart.originalString.substring(0, num + 1) + relativePart;
            num = basePart.originalString.indexOf(':', num + 1);
            return basePart.originalString.substring(0, num + 1) + relativePart;
        }

        if (basePart._syntax!.inFact(S.FileLikeUri) && (c === BACKSLASH || c === SLASH)) {
            if (relativePart.length >= 2 && (relativePart.charCodeAt(1) === BACKSLASH || relativePart.charCodeAt(1) === SLASH)) {
                return !basePart.isImplicitFile ? 'file:' + relativePart : relativePart;
            }

            if (basePart.isUnc) {
                let path = basePart.getParts(UriComponents.Path | UriComponents.KeepDelimiter, UriFormat.Unescaped);
                const slash = path.indexOf('/', 1);
                if (slash >= 0) path = path.substring(0, slash);
                if (basePart.isImplicitFile) return '\\\\' + basePart.getParts(UriComponents.Host, UriFormat.Unescaped) + path + relativePart;
                return 'file://' + basePart.getParts(UriComponents.Host, uriFormat) + path + relativePart;
            }

            return 'file://' + relativePart;
        }

        const convertPathSlashes = basePart._syntax!.inFact(S.ConvertPathSlashes);
        let text: string;
        if (c === SLASH || (c === BACKSLASH && convertPathSlashes)) {
            if (relativePart.length >= 2 && relativePart.charCodeAt(1) === SLASH) return basePart.scheme + ':' + relativePart;
            text = basePart.hostType !== F.IPv6HostType
                ? basePart.getParts(UriComponents.SchemeAndServer | UriComponents.UserInfo, uriFormat)
                : `${basePart.getParts(UriComponents.Scheme | UriComponents.UserInfo, uriFormat)}[${basePart.ipv6DnsSafeHost}]${basePart.getParts(UriComponents.Port | UriComponents.KeepDelimiter, uriFormat)}`;
            if (!convertPathSlashes || c !== BACKSLASH) return text + relativePart;
            return text + '/' + relativePart.substring(1);
        }

        text = basePart.getParts(UriComponents.Path | UriComponents.KeepDelimiter, basePart.isImplicitFile ? UriFormat.Unescaped : uriFormat);
        let length = text.length;
        const array: number[] = new Array<number>(length + relativePart.length).fill(0);
        if (length > 0) {
            for (let k = 0; k < length; k++) array[k] = text.charCodeAt(k);
            while (length > 0) {
                if (array[--length] === SLASH) {
                    length++;
                    break;
                }
            }
        }

        for (let k = 0; k < relativePart.length; k++) array[length + k] = relativePart.charCodeAt(k);
        c = basePart._syntax!.inFact(S.MayHaveQuery) ? QUESTION : NOCHAR;
        const c2 = !basePart.isImplicitFile && basePart._syntax!.inFact(S.MayHaveFragment) ? HASH : NOCHAR;
        let rest = '';
        if (c !== NOCHAR || c2 !== NOCHAR) {
            let i: number;
            for (i = 0; i < relativePart.length && array[length + i] !== c && array[length + i] !== c2; i++) { /* to the query or fragment */ }
            if (i === 0) {
                rest = relativePart;
            } else if (i < relativePart.length) {
                rest = relativePart.substring(i);
            }

            length += i;
        } else {
            length += relativePart.length;
        }

        const fromChars = (from: number, to: number) => String.fromCharCode(...array.slice(from, to));
        if (basePart.hostType === F.IPv6HostType) {
            text = !basePart.isImplicitFile
                ? basePart.getParts(UriComponents.Scheme | UriComponents.UserInfo, uriFormat) + '[' + basePart.ipv6DnsSafeHost + ']' + basePart.getParts(UriComponents.Port | UriComponents.KeepDelimiter, uriFormat)
                : '\\\\[' + basePart.ipv6DnsSafeHost + ']';
        } else if (basePart.isImplicitFile) {
            if (basePart.isDosPath) {
                length = 3 + compress(array, 3, length - 3, basePart._syntax!);
                return fromChars(1, length) + rest;
            }

            text = !basePart.isUnixPath ? '\\\\' + basePart.getParts(UriComponents.Host, UriFormat.Unescaped) : basePart.getParts(UriComponents.Host, UriFormat.Unescaped);
        } else {
            text = basePart.getParts(UriComponents.SchemeAndServer | UriComponents.UserInfo, uriFormat);
        }

        const secured = basePart.securedPathIndex;
        length = secured + compress(array, secured, length - secured, basePart._syntax!);
        return text + fromChars(0, length) + rest;
    }
}

/** Uri.CreateHostStringHelper */
function createHostStringHelper(str: string, idx: number, end: number, flags: Ref<bigint>, scopeId: Ref<string | null>): string {
    const loopback = { value: false };
    let text: string;
    switch (flags.value & F.HostTypeMask) {
        case F.DnsHostType:
            text = domainParseCanonicalName(str, idx, end, loopback);
            break;
        case F.IPv6HostType:
            text = ipv6ParseCanonicalName(str, idx, loopback, scopeId);
            break;
        case F.IPv4HostType:
            text = ipv4ParseCanonicalName(str, idx, end, loopback);
            break;
        case F.UncHostType:
            text = domainParseCanonicalName(str, idx, end, loopback);
            break;
        case F.BasicHostType:
            text = (flags.value & F.DosPath) === 0n ? str.substring(idx, end) : '';
            if (text.length === 0) loopback.value = true;
            break;
        case F.HostTypeMask:
            text = '';
            break;
        default:
            throw getException(ParsingError.BadHostName)!;
    }

    if (loopback.value) flags.value |= F.LoopbackHost;
    return text;
}

/** Uri.GetLengthWithoutTrailingSpaces */
function getLengthWithoutTrailingSpaces(str: string, length: Ref<number>, idx: number): void {
    let end = length.value;
    while (end > idx && isLWS(ch(str, end - 1))) end--;
    length.value = end;
}
