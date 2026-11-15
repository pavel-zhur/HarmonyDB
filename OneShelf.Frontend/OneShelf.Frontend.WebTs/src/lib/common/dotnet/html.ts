// System.Web.HttpUtility encoders as .NET (Core) implements them.

/**
 * HttpUtility.HtmlEncode (= WebUtility.HtmlEncode): &lt; &gt; &quot; &#39; &amp;, characters 160–255 as &#NNN;,
 * characters outside the BMP as &#NNNNN; (a lone surrogate becomes U+FFFD).
 */
export function htmlEncode(value: string | null): string | null {
    if (value == null || value.length === 0) return value;
    let result = '';
    for (let i = 0; i < value.length; i++) {
        const ch = value.charCodeAt(i);
        if (ch <= 0x3e) {
            switch (ch) {
                case 0x3c: result += '&lt;'; break;
                case 0x3e: result += '&gt;'; break;
                case 0x22: result += '&quot;'; break;
                case 0x27: result += '&#39;'; break;
                case 0x26: result += '&amp;'; break;
                default: result += value[i]; break;
            }
        } else if (ch >= 160 && ch < 256) {
            result += `&#${ch};`;
        } else if (ch >= 0xd800 && ch <= 0xdfff) {
            const next = i + 1 < value.length ? value.charCodeAt(i + 1) : 0;
            if (ch <= 0xdbff && next >= 0xdc00 && next <= 0xdfff) {
                result += `&#${(ch - 0xd800) * 0x400 + (next - 0xdc00) + 0x10000};`;
                i++;
            } else {
                result += '�';
            }
        } else {
            result += value[i];
        }
    }
    return result;
}

/** HttpUtility.HtmlAttributeEncode: &quot; &#39; &amp; &lt; only. */
export function htmlAttributeEncode(value: string | null | undefined): string | null {
    if (value == null) return null;
    if (value.length === 0) return value;
    let result = '';
    for (let i = 0; i < value.length; i++) {
        const c = value[i]!;
        switch (c) {
            case '"': result += '&quot;'; break;
            case '\'': result += '&#39;'; break;
            case '&': result += '&amp;'; break;
            case '<': result += '&lt;'; break;
            default: result += c; break;
        }
    }
    return result;
}
