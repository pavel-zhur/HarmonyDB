import { beforeAll, describe, expect, it, vi } from 'vitest';
import { createMemoryRouter } from 'react-router-dom';
import type { LocationChangedEventArgs } from './navigation';

// The module reads the browser's address when it loads; the router here keeps its history in memory.
let navigation: typeof import('./navigation');

beforeAll(async () => {
    const baseURI = 'http://localhost/';
    vi.stubGlobal('location', { href: 'http://localhost/collection', origin: 'http://localhost', pathname: '/collection', search: '' });
    vi.stubGlobal('document', {
        baseURI,
        createElement: () => {
            let href = '';
            return {
                get href() { return href; },
                set href(value: string) { href = new URL(value, baseURI).href; },
            };
        },
    });
    navigation = await import('./navigation');
});

describe('LocationChanged', () => {
    it('is raised once for every navigation, also to the address already shown, and for back/forward', () => {
        const routes = [{ path: '/collection' }, { path: '/artists' }];
        const router = createMemoryRouter(routes, { initialEntries: ['/collection'] });
        navigation.attachRouter(router as never, routes);
        const events: LocationChangedEventArgs[] = [];
        const unsubscribe = navigation.navigationManager.locationChanged(args => events.push(args));

        navigation.navigationManager.navigateTo('/collection');
        navigation.navigationManager.navigateTo('/collection', false, true);
        navigation.navigationManager.navigateTo('/artists?x=1');
        void router.navigate(-1);

        expect(events).toEqual([
            { location: 'http://localhost/collection', isNavigationIntercepted: false },
            { location: 'http://localhost/collection', isNavigationIntercepted: false },
            { location: 'http://localhost/artists?x=1', isNavigationIntercepted: false },
            { location: 'http://localhost/collection', isNavigationIntercepted: true },
        ]);
        expect(navigation.navigationManager.uri).toBe('http://localhost/collection');

        // The router's other updates (not navigations) raise nothing.
        router.revalidate();
        expect(events.length).toBe(4);
        unsubscribe();
    });
});
