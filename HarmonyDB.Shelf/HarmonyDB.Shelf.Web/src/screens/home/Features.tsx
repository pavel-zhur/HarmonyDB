// A few things worth trying, each a card that leads to where it is done — on the home after the chords and the
// playlists, and where a guest closed the chords. Only what works and is open to this person (a guest: what a sign-in
// opens to everyone); the whole list is "What the shelf can do".

import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useConfiguration, useSession } from '../../data/session';
import { useAssistantsOpen } from '../../data/mcp';
import { Visual, type Card } from './GuestHome';
import './home.css';

export function Features() {
    const { t } = useTranslation();
    const { session } = useSession();
    const configuration = useConfiguration();
    const assistants = useAssistantsOpen();
    const permissions = session?.permissions ?? [];
    const showcase = configuration.data?.showcasePlaylistId ?? null;
    const cards: { key: Card; to: string }[] = [
        { key: 'importPlaylist', to: '/playlists/import' },
        ...(showcase ? [{ key: 'playAlong' as const, to: `/playlists/${showcase}` }] : []),
        { key: 'import', to: '/import' },
        { key: 'fingerings', to: '/fingerings' },
        ...(permissions.includes('pdf') ? [{ key: 'book' as const, to: '/playlists' }] : []),
        ...(assistants ? [{ key: 'ai' as const, to: '/ai' }] : []),
    ];
    return (
        <>
            <h2>{t('guest.whatHere')}</h2>
            <div className="fgridc few">
                {cards.slice(0, 5).map(card => (
                    <Link key={card.key} className="fcard go" to={card.to}>
                        <span className="fvis"><Visual card={card.key} /></span>
                        <span className="fbody">
                            <b>{t(`guest.cards.${card.key}.title`)}</b>
                            <small>{t(`guest.cards.${card.key}.text`)}</small>
                        </span>
                    </Link>
                ))}
            </div>
        </>
    );
}
