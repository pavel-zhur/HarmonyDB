// The navigation, in one place: the main sections (bottom tabs on a phone, the side menu's top wide), the tools and
// the rest (the side menu wide, "More" on a phone). A new screen that belongs in the menu is a line here.

import type { IconName } from '../kit/Icon';
import type { en } from '../i18n/en';

type Key<T> = { [K in keyof T]: T[K] extends string ? K : never }[keyof T];
export type NavKey = `nav.${Key<typeof en.nav>}`;
export type ToolKey = `tools.${Key<typeof en.tools>}`;

export interface NavItem {
    to: string;
    icon: IconName;
    label: NavKey | ToolKey;
    sub?: ToolKey;
    /** README "soon": shown, marked, does nothing */
    soon?: boolean;
    /** the address is one of its section's (its tab is lit on them too) */
    match?: string[];
    /** shown only to whom it is open: the person's permissions (a guest has none) */
    open?: (permissions: readonly string[]) => boolean;
}

export const mainNav: NavItem[] = [
    { to: '/', icon: 'home', label: 'nav.home' },
    { to: '/library', icon: 'lib', label: 'nav.library' },
    { to: '/playlists', icon: 'pls', label: 'nav.playlists' },
    { to: '/recent', icon: 'clock', label: 'nav.recent' },
];

export const toolsNav: NavItem[] = [
    { to: '/import', icon: 'imp', label: 'tools.import', sub: 'tools.importSub' },
    { to: '/analysis', icon: 'ai', label: 'tools.analysis', sub: 'tools.analysisSub', soon: true },
    { to: '/editor', icon: 'pen', label: 'tools.editor', sub: 'tools.editorSub' },
    { to: '/illustrations', icon: 'img', label: 'tools.illustrations', sub: 'tools.illustrationsSub' },
    { to: '/fingerings', icon: 'frets', label: 'tools.fingerings', sub: 'tools.fingeringsSub' },
    { to: '/tuner', icon: 'gauge', label: 'tools.tuner', sub: 'tools.tunerSub' },
    { to: '/ai', icon: 'mcp', label: 'tools.mcp', sub: 'tools.mcpSub', open: permissions => permissions.some(p => p.startsWith('mcp.')) },
];

/** The items open to whom has these permissions. */
export function openTo<T extends NavItem>(items: readonly T[], permissions: readonly string[] | undefined): T[] {
    return items.filter(item => !item.open || item.open(permissions ?? []));
}

export const otherNav: NavItem[] = [
    { to: '/offline', icon: 'cloud', label: 'tools.offline', sub: 'tools.offlineSub' },
    { to: '/profile', icon: 'user', label: 'nav.profile', sub: 'tools.profileSub' },
    { to: '/about', icon: 'info', label: 'tools.about', sub: 'tools.aboutSub' },
    { to: '/versions', icon: 'clock', label: 'tools.versions', sub: 'tools.versionsSub' },
];

/** A guest's: the search (home) and signing in; the tools and two of the rest. */
export const guestOtherNav: NavItem[] = otherNav.filter(x => x.to === '/about' || x.to === '/versions');

/** The addresses of "More" (its tab is lit on them). */
export const moreAddresses = ['/more', ...toolsNav.map(x => x.to), ...otherNav.map(x => x.to)];

export function isOn(item: NavItem, pathname: string): boolean {
    if (item.to === '/') return pathname === '/';
    return pathname === item.to || pathname.startsWith(item.to + '/') || !!item.match?.some(m => pathname.startsWith(m));
}
