// "How a guest sees it" (/s/:token/guest[?chords=…], from the share box): the link as a guest opens it — the whole
// app, started as a guest's in a frame of this page (app/guestMode.ts: it is a guest's only when framed by this page),
// so every screen is exactly what a guest gets. Above the frame, a plain band says what this is, with the way back.
// The person stays signed in: nothing of this page changes.

import { useNavigate, useParams, useSearchParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../kit/Icon';
import '../song/song.css';

export function GuestPreview() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { token } = useParams() as { token: string };
    const [query] = useSearchParams();
    const chords = query.get('chords');
    const address = `/s/${encodeURIComponent(token)}${chords ? `?chords=${encodeURIComponent(chords)}` : ''}`;
    return (
        <div className="guestpreview">
            <div className="guestframe">
                <Icon name="eye" /><span>{t('song.share.guest.frame')}</span>
                <button type="button" className="btn sm" onClick={() => navigate(-1)}>{t('song.share.guest.back')}</button>
            </div>
            <iframe title={t('song.share.guest.frame')} src={address} />
        </div>
    );
}
