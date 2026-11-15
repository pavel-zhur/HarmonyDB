// The original: Components/CollectionNavLink.cs — an <a> with the "active" class (or ActiveClass) while the address
// is /collection (or below it, with Match Prefix) and, depending on IsMy, does or does not contain
// "ShortlistedUserId={my id}"; never active without an identity.
//
// Use: <CollectionNavLink isMy={...} className="..." href="..." ...>children</CollectionNavLink> — every other
// attribute goes to the <a> (CaptureUnmatchedValues), `className` is the Razor `class`.

import { useLayoutEffect, useRef, type AnchorHTMLAttributes, type ReactNode } from 'react';
import { navigationManager } from '../framework/navigation';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { services } from '../services/registry';
import { combineWithSpace, equalsHrefExactlyOrIfTrailingSlashAdded, isStrictlyPrefixWithSeparator, NavLinkMatch } from './navLinkMatching';

const DefaultActiveClass = 'active';

export interface CollectionNavLinkProps extends Omit<AnchorHTMLAttributes<HTMLAnchorElement>, 'children'> {
    activeClass?: string | null;
    isMy: boolean;
    match?: NavLinkMatch;
    children?: ReactNode;
}

export function CollectionNavLink({ activeClass, isMy, match = NavLinkMatch.Prefix, children, ...additionalAttributes }: CollectionNavLinkProps) {
    const identityProvider = services.identityProvider;
    const collectionNavigation = services.collectionNavigation;
    const stateHasChanged = useStateHasChanged();

    const shouldMatch = (currentUriAbsolute: string, hrefAbsolute: string | null): boolean => {
        if (hrefAbsolute === null) {
            return false;
        }

        if (identityProvider.identity === null) return false;
        if (currentUriAbsolute.includes(collectionNavigation.getMyContainedString()) !== isMy) return false;

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
    const hrefAbsolute = navigationManager.toAbsoluteUri('collection');
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
        <a {...additionalAttributes} className={cssClass ?? undefined}>
            {children}
        </a>
    );
}
