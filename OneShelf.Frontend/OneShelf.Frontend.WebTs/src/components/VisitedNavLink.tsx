// The original: Components/VisitedNavLink.cs — an <a> with the "active" class (or ActiveClass) while the address is
// /visitedsearch or /visitedchords (or below, with Match Prefix); never active without an href.
//
// Use: <VisitedNavLink className="..." href="..." ...>children</VisitedNavLink> — every other attribute goes to the
// <a> (CaptureUnmatchedValues), `className` is the Razor `class`.

import { useLayoutEffect, useRef, type AnchorHTMLAttributes, type ReactNode } from 'react';
import { navigationManager } from '../framework/navigation';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { combineWithSpace, equalsHrefExactlyOrIfTrailingSlashAdded, isStrictlyPrefixWithSeparator, NavLinkMatch } from './navLinkMatching';

const DefaultActiveClass = 'active';

export interface VisitedNavLinkProps extends Omit<AnchorHTMLAttributes<HTMLAnchorElement>, 'children'> {
    activeClass?: string | null;
    match?: NavLinkMatch;
    children?: ReactNode;
}

export function VisitedNavLink({ activeClass, match = NavLinkMatch.Prefix, children, ...additionalAttributes }: VisitedNavLinkProps) {
    const stateHasChanged = useStateHasChanged();

    const shouldMatch = (currentUriAbsolute: string, hrefAbsolute: string, all: readonly string[] | null): boolean => {
        if (all === null) {
            return false;
        }

        if (equalsHrefExactlyOrIfTrailingSlashAdded(currentUriAbsolute, hrefAbsolute)) {
            return true;
        }

        if (match === NavLinkMatch.Prefix
            && isStrictlyPrefixWithSeparator(currentUriAbsolute, hrefAbsolute)) {
            return true;
        }

        return false;
    };

    // OnParametersSet
    const href = additionalAttributes.href ?? null;
    const hrefAbsolute = href === null
        ? null
        : [
            navigationManager.toAbsoluteUri('visitedsearch'),
            navigationManager.toAbsoluteUri('visitedchords'),
        ];
    const isActiveAt = (uri: string) => hrefAbsolute?.some(h => shouldMatch(uri, h, hrefAbsolute)) ?? false;

    const state = useRef({ isActive: false, isActiveAt });
    state.current.isActiveAt = isActiveAt;
    state.current.isActive = isActiveAt(navigationManager.uri);

    const className = additionalAttributes.className ?? null;
    const cssClass = state.current.isActive ? combineWithSpace(className, activeClass ?? DefaultActiveClass) : className;

    // OnInitialized: NavigationManager.LocationChanged += OnLocationChanged; Dispose: -=
    useLayoutEffect(() => navigationManager.locationChanged(args => {
        const shouldBeActiveNow = state.current.isActiveAt(args.location);
        if (shouldBeActiveNow !== state.current.isActive) {
            state.current.isActive = shouldBeActiveNow;
            stateHasChanged();
        }
    }), [stateHasChanged]);

    return (
        <a {...additionalAttributes} className={cssClass ?? undefined}>
            {children}
        </a>
    );
}
