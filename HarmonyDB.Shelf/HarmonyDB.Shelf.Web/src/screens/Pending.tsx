// The place of a screen another unit builds (the menu leads here already): its title only. Replace the route's
// element in app/routes.tsx with the screen.

import { useTranslation } from 'react-i18next';
import { Page } from '../app/panes';
import type { NavKey, ToolKey } from '../app/nav';

export function Pending({ title }: { title: NavKey | ToolKey | 'playlist.book' }) {
    const { t } = useTranslation();
    return <Page><h1>{t(title)}</h1></Page>;
}
