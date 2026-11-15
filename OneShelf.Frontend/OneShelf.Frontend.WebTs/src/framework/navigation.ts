// Blazor's navigation (NavigationManager and its JavaScript side, Web.JS NavigationManager.ts/NavigationUtils.ts,
// .NET 9) over the React Router data router:
//
// - Clicks on any <a href> within the base URI are handled inside the application, as Blazor's router intercepts them:
//   not with a mouse button other than the left one, not with Ctrl/Shift/Alt/Meta, not when the click was already
//   handled (preventDefault), not with a target other than _self, not with download. A link to the same page with a
//   #hash only scrolls to that element. A link to an address no page has loads that address from the server (Blazor's
//   router does so for an intercepted navigation it cannot match).
// - navigationManager.navigateTo(uri, forceLoad, replace) is NavigationManager.NavigateTo.
// - After a navigation inside the application (not back/forward), the window is scrolled to the top once the new page
//   has rendered.
// - navigationManager.uri / baseUri / locationChanged / toAbsoluteUri / toBaseRelativePath are the NavigationManager's.

import type { createBrowserRouter } from 'react-router-dom';
import { matchRoutes, type RouteObject } from 'react-router-dom';
import { addAfterClickCallback } from './eventDelegator';

type DataRouter = ReturnType<typeof createBrowserRouter>;

export interface LocationChangedEventArgs {
    location: string;
    isNavigationIntercepted: boolean;
}

type LocationChangedListener = (args: LocationChangedEventArgs) => void;

let router: DataRouter | undefined;
let pageRoutes: RouteObject[] = [];
let uri = location.href;
let pendingIntercepted = false;
let lastNavigationIntercepted = false;
let resetScrollAfterNextRender = false;
const locationChangedListeners = new Set<LocationChangedListener>();

export const navigationManager = {
    get uri(): string {
        return uri;
    },

    get baseUri(): string {
        return document.baseURI;
    },

    /** Subscribes to LocationChanged; returns the unsubscription. */
    locationChanged(listener: LocationChangedListener): () => void {
        locationChangedListeners.add(listener);
        return () => {
            locationChangedListeners.delete(listener);
        };
    },

    navigateTo(target: string, forceLoad = false, replace = false): void {
        const absoluteUri = toAbsoluteUri(target);
        if (!forceLoad && isWithinBaseUriSpace(absoluteUri)) {
            performInternalNavigation(absoluteUri, false, replace);
        } else {
            performExternalNavigation(target, replace);
        }
    },

    toAbsoluteUri,

    toBaseRelativePath(target: string): string {
        const baseUri = document.baseURI;
        if (target.startsWith(baseUri)) {
            return target.substring(baseUri.length);
        }

        const pathEndIndex = target.search(/[#?]/);
        const pathOnly = pathEndIndex < 0 ? target : target.substring(0, pathEndIndex);
        if (baseUri.endsWith('/') && pathOnly === baseUri.substring(0, baseUri.length - 1)) {
            return target.substring(baseUri.length - 1);
        }

        throw new Error(`The URI '${target}' is not contained by the base URI '${baseUri}'.`);
    },
};

/** Connects the navigation to the router; called once, when the router is created. */
export function attachRouter(dataRouter: DataRouter, routes: RouteObject[]): void {
    router = dataRouter;
    pageRoutes = routes;
    // Blazor raises LocationChanged once for every navigation it performs (a link, NavigateTo, with or without
    // replace) and for every back/forward, also when the address stays the same. The router gives each completed
    // navigation, POP included, a new location object (synchronously, as no route has a loader), and keeps that object
    // in its further updates; so a new object is one navigation.
    let lastLocation = dataRouter.state.location;
    dataRouter.subscribe(state => {
        if (state.location === lastLocation) return;
        lastLocation = state.location;
        uri = new URL(dataRouter.createHref(state.location), document.baseURI).href;
        // Back/forward is reported as intercepted, as Blazor's popstate handler does.
        const isNavigationIntercepted = state.historyAction === 'POP' || pendingIntercepted;
        pendingIntercepted = false;
        lastNavigationIntercepted = isNavigationIntercepted;
        for (const listener of [...locationChangedListeners]) {
            listener({ location: uri, isNavigationIntercepted });
        }
    });
}

/**
 * Starts intercepting link clicks; Blazor's router does so after its first render. As in Blazor, a click is looked at
 * in the application's click listener, after the application's click handlers (eventDelegator.ts).
 */
export function enableNavigationInterception(): () => void {
    return addAfterClickCallback(handleClickForNavigationInterception);
}

/** Called after every render of the router's content: scrolls to the top once after a navigation inside the application. */
export function resetScrollIfNeeded(): void {
    if (resetScrollAfterNextRender) {
        resetScrollAfterNextRender = false;
        window.scrollTo(0, 0);
    }
}

/**
 * For an address no page has: true when it was reached by back/forward (Blazor treats that as an intercepted
 * navigation, and its router then loads the address from the server instead of showing "not found").
 */
export function isUnknownAddressToLoad(): boolean {
    return lastNavigationIntercepted;
}

export function loadFromServer(): void {
    performExternalNavigation(location.href, false);
}

function handleClickForNavigationInterception(event: MouseEvent): void {
    if (event.button !== 0 || event.ctrlKey || event.shiftKey || event.altKey || event.metaKey) {
        return;
    }

    if (event.defaultPrevented) {
        return;
    }

    const anchorTarget = findAnchorTarget(event);
    if (anchorTarget && canProcessAnchor(anchorTarget)) {
        const absoluteHref = toAbsoluteUri(anchorTarget.getAttribute('href')!);
        if (isWithinBaseUriSpace(absoluteHref)) {
            event.preventDefault();
            performInternalNavigation(absoluteHref, true, false);
        }
    }
}

function performInternalNavigation(absoluteInternalHref: string, interceptedLink: boolean, replace: boolean): void {
    if (router === undefined) {
        throw new Error('The router is not attached yet.');
    }

    if (isSamePageWithHash(absoluteInternalHref)) {
        // Only the browser's history and the scroll position change; the application is not told.
        if (replace) {
            history.replaceState(history.state, '', absoluteInternalHref);
        } else {
            history.pushState(history.state, '', absoluteInternalHref);
        }

        const hashIndex = absoluteInternalHref.indexOf('#');
        if (hashIndex !== absoluteInternalHref.length - 1) {
            document.getElementById(absoluteInternalHref.substring(hashIndex + 1))?.scrollIntoView();
        }

        return;
    }

    const url = new URL(absoluteInternalHref);
    if (interceptedLink && matchRoutes(pageRoutes, url.pathname) === null) {
        if (!replace) history.pushState(null, '', absoluteInternalHref);
        performExternalNavigation(absoluteInternalHref, false);
        return;
    }

    resetScrollAfterNextRender = true;
    pendingIntercepted = interceptedLink;
    void router.navigate(url.pathname + url.search + url.hash, { replace });
}

function performExternalNavigation(target: string, replace: boolean): void {
    if (location.href === target) {
        // Reloading the address the browser is at (see dotnet/aspnetcore#10839).
        history.replaceState(null, '', target + '?');
        location.replace(target);
    } else if (replace) {
        location.replace(target);
    } else {
        location.href = target;
    }
}

function isWithinBaseUriSpace(href: string): boolean {
    const baseUri = document.baseURI;
    const baseUriWithoutTrailingSlash = baseUri.substring(0, baseUri.lastIndexOf('/'));
    const nextChar = href.charAt(baseUriWithoutTrailingSlash.length);
    return href.startsWith(baseUriWithoutTrailingSlash)
        && (nextChar === '' || nextChar === '/' || nextChar === '?' || nextChar === '#');
}

function isSamePageWithHash(absoluteHref: string): boolean {
    const url = new URL(absoluteHref);
    return url.hash !== '' && location.origin === url.origin && location.pathname === url.pathname && location.search === url.search;
}

let testAnchor: HTMLAnchorElement | undefined;

function toAbsoluteUri(relativeUri: string): string {
    testAnchor ??= document.createElement('a');
    testAnchor.href = relativeUri;
    return testAnchor.href;
}

function canProcessAnchor(anchorTarget: HTMLAnchorElement | SVGAElement): boolean {
    const targetAttributeValue = anchorTarget.getAttribute('target');
    const opensInSameFrame = !targetAttributeValue || targetAttributeValue === '_self';
    return opensInSameFrame && anchorTarget.hasAttribute('href') && !anchorTarget.hasAttribute('download');
}

function findAnchorTarget(event: MouseEvent): HTMLAnchorElement | SVGAElement | null {
    for (const candidate of event.composedPath()) {
        if (candidate instanceof HTMLAnchorElement || candidate instanceof SVGAElement) {
            return candidate;
        }
    }

    return null;
}
