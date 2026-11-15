// Blazor's NavLink (Microsoft.AspNetCore.Components.Routing.NavLink, .NET 9) — an <a> with the "active" class (or
// ActiveClass) and aria-current="page" while the address matches its href: exactly (or with a trailing slash added),
// or below it with Match Prefix; never active without an href. (With Match All an address with a query does not
// match: .NET 9's ignoring of the query and fragment for Match All is not in effect in the original, as seen there.)
//
// Use: <NavLink className="..." href="..." match={NavLinkMatch.All} ...>children</NavLink> — every other attribute goes
// to the <a> (CaptureUnmatchedValues), `className` is the Razor `class`.

import { useLayoutEffect, useRef, type AnchorHTMLAttributes, type ReactNode } from 'react';
import { navigationManager } from '../framework/navigation';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { combineWithSpace, equalsHrefExactlyOrIfTrailingSlashAdded, isStrictlyPrefixWithSeparator, NavLinkMatch } from './navLinkMatching';

const DefaultActiveClass = 'active';

export interface NavLinkProps extends Omit<AnchorHTMLAttributes<HTMLAnchorElement>, 'children'> {
    activeClass?: string | null;
    match?: NavLinkMatch;
    children?: ReactNode;
}

export function NavLink({ activeClass, match = NavLinkMatch.Prefix, children, ...additionalAttributes }: NavLinkProps) {
    const stateHasChanged = useStateHasChanged();

    const shouldMatch = (uriAbsolute: string, hrefAbsolute: string | null): boolean => {
        if (hrefAbsolute === null) {
            return false;
        }

        if (equalsHrefExactlyOrIfTrailingSlashAdded(uriAbsolute, hrefAbsolute)) {
            return true;
        }

        if (match === NavLinkMatch.Prefix
            && isStrictlyPrefixWithSeparator(uriAbsolute, hrefAbsolute)) {
            return true;
        }

        return false;
    };

    // OnParametersSet
    const href = additionalAttributes.href ?? null;
    const hrefAbsolute = href === null ? null : navigationManager.toAbsoluteUri(href);
    const state = useRef({ isActive: false, hrefAbsolute: null as string | null, shouldMatch });
    state.current.hrefAbsolute = hrefAbsolute;
    state.current.shouldMatch = shouldMatch;
    state.current.isActive = shouldMatch(navigationManager.uri, hrefAbsolute);

    const className = additionalAttributes.className ?? null;
    const cssClass = state.current.isActive ? combineWithSpace(className, activeClass ?? DefaultActiveClass) : className;

    // OnInitialized: NavigationManager.LocationChanged += OnLocationChanged; Dispose: -=
    useLayoutEffect(() => navigationManager.locationChanged(args => {
        const shouldBeActiveNow = state.current.shouldMatch(args.location, state.current.hrefAbsolute);
        if (shouldBeActiveNow !== state.current.isActive) {
            state.current.isActive = shouldBeActiveNow;
            stateHasChanged();
        }
    }), [stateHasChanged]);

    return (
        <a {...additionalAttributes} className={cssClass ?? undefined} aria-current={state.current.isActive ? 'page' : undefined}>
            {children}
        </a>
    );
}
