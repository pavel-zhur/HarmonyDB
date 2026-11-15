// What CollectionNavLink.cs and VisitedNavLink.cs share (each has its own copy in the original, from Blazor's NavLink).

/** Microsoft.AspNetCore.Components.Routing.NavLinkMatch */
export enum NavLinkMatch {
    Prefix = 0,
    All = 1,
}

/** char.IsLetterOrDigit */
function isLetterOrDigit(c: string | undefined): boolean {
    return c !== undefined && /^[\p{L}\p{Nd}]$/u.test(c);
}

function equalsIgnoreCase(a: string, b: string): boolean {
    return a.length === b.length && upperInvariant(a) === upperInvariant(b);
}

function upperInvariant(value: string): string {
    let result = '';
    for (const c of value) {
        const upper = c.toUpperCase();
        result += upper.length === 1 ? upper : c;
    }

    return result;
}

export function equalsHrefExactlyOrIfTrailingSlashAdded(currentUriAbsolute: string, hrefAbsolute: string): boolean {
    if (equalsIgnoreCase(currentUriAbsolute, hrefAbsolute)) {
        return true;
    }

    if (currentUriAbsolute.length === hrefAbsolute.length - 1) {
        // Special case: highlight links to http://host/path/ even if you're
        // at http://host/path (with no trailing slash)
        if (hrefAbsolute[hrefAbsolute.length - 1] === '/'
            && upperInvariant(hrefAbsolute).startsWith(upperInvariant(currentUriAbsolute))) {
            return true;
        }
    }

    return false;
}

export function isStrictlyPrefixWithSeparator(value: string, prefix: string): boolean {
    const prefixLength = prefix.length;
    if (value.length > prefixLength) {
        return upperInvariant(value).startsWith(upperInvariant(prefix))
            && (
                // Only match when there's a separator character either at the end of the
                // prefix or right after it.
                prefixLength === 0
                || !isLetterOrDigit(prefix[prefixLength - 1])
                || !isLetterOrDigit(value[prefixLength])
            );
    }

    return false;
}

export function combineWithSpace(str1: string | null, str2: string): string {
    return str1 === null ? str2 : `${str1} ${str2}`;
}
