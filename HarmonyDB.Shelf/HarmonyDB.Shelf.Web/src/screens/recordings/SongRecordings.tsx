// A song's recordings, on its page: found automatically on both services and shown as what they are — guesses, each
// with its service's mark. A tap chooses the one to listen to. "Find another recording" shows the candidates: the wrong
// ones are removed, the missing ones added — in this playlist only.
//
//   <SongRecordings song={{ playlistId, songId, share }} analysed={…} />
//   analysed: the song has an analysis of a recording (else "Analyse this recording" stands here, marked "soon")

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import type { MusicTrack } from '../../data/music';
import { addRecording, chooseRecording, findRecordings, removeRecording, useSongRecordings, type SongRecordings as Recordings } from '../../data/recordings';
import { useSession } from '../../data/session';
import { Soon } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { MenuTitle, Popover } from '../../kit/Popover';
import { ServiceMark, artistNames, clock } from '../music/parts';
import { chooseHere, useChosenRecording } from './choice';
import './recordings.css';

type SongPath = { playlistId: string; songId: string; share?: string | null };

/** How many recordings are shown before "more". */
const shownFirst = 3;

export function SongRecordings({ song, analysed }: { song: SongPath; analysed: boolean }) {
    const { t } = useTranslation();
    const { signedIn } = useSession();
    const recordings = useSongRecordings(song);
    const chosen = useChosenRecording(recordings.data);
    const [all, setAll] = useState(false);
    const [anchor, setAnchor] = useState<HTMLElement | null>(null);
    const data = recordings.data;
    if (!data) return recordings.isError ? <p className="failed">{errorText(recordings.error)}</p> : null;

    const choose = (itemId: string) => {
        chooseHere(song, itemId);
        if (signedIn) void chooseRecording({ song, itemId });
    };
    const list = data.recordings;
    const shown = all ? list : list.slice(0, shownFirst);
    if (chosen && !shown.includes(chosen)) shown.push(chosen);

    return (
        <div className="rec-list">
            <div className="rec-lab">{t('recordings.title')}</div>
            {list.length === 0 && <p className="muted small">{t('recordings.none')}</p>}
            {shown.map(r => (
                <button type="button" key={r.itemId} className={r === chosen ? 'rec-row on' : 'rec-row'} aria-pressed={r === chosen} onClick={() => choose(r.itemId)}>
                    <RecordingLine track={r.track} />
                </button>
            ))}
            <div className="links">
                {list.length > shown.length && <button type="button" className="lnk" onClick={() => setAll(true)}>{t('recordings.more', { count: list.length - shown.length })}</button>}
                <button type="button" className="lnk" onClick={e => setAnchor(anchor ? null : e.currentTarget)}>{t('recordings.findAnother')}</button>
                {!analysed && chosen && <span className="lnk off"><Icon name="ai" />{t('music.analyse')} <Soon /></span>}
            </div>
            <Popover anchor={anchor} onClose={() => setAnchor(null)} width={380} label={t('recordings.findAnother')}>
                <FindAnother song={song} data={data} />
            </Popover>
        </div>
    );
}

function RecordingLine({ track }: { track: MusicTrack }) {
    const sub = [artistNames(track.artists), track.version, track.album?.title].filter(Boolean).join(' · ');
    return (
        <>
            {track.coverUrl ? <img className="rec-cov" src={track.coverUrl} alt="" loading="lazy" referrerPolicy="no-referrer" /> : <span className="rec-cov" />}
            <span className="rec-t1">
                <b>{track.title}</b>
                <span><ServiceMark service={track.service} /> {sub}</span>
            </span>
            {track.durationMs ? <span className="d">{clock(track.durationMs / 1000)}</span> : null}
        </>
    );
}

/** The song's recordings and the candidates found: remove the wrong, add the missing; look again on the services. */
function FindAnother({ song, data }: { song: SongPath; data: Recordings }) {
    const { t } = useTranslation();
    const { signedIn } = useSession();
    const find = useMutation({ mutationFn: findRecordings });
    const add = useMutation({ mutationFn: addRecording });
    const remove = useMutation({ mutationFn: removeRecording });
    const busy = add.isPending || remove.isPending;
    return (
        <>
            <MenuTitle>{t('recordings.ofSong')}</MenuTitle>
            {data.recordings.map(r => (
                <div className="it rec-it" key={r.itemId}>
                    <RecordingLine track={r.track} />
                    {r.canRemove && (
                        <button type="button" className="xbtn" disabled={busy} aria-label={t('recordings.remove')} title={t('recordings.remove')}
                            onClick={() => remove.mutate({ song, itemId: r.itemId })}><Icon name="x" /></button>
                    )}
                </div>
            ))}
            {data.candidates.length > 0 && (
                <>
                    <hr />
                    <MenuTitle>{t('recordings.alsoFound')}</MenuTitle>
                    {data.candidates.map(track => (
                        <div className="it rec-it" key={`${track.service}:${track.id}`}>
                            <RecordingLine track={track} />
                            {data.canAdd && (
                                <button type="button" className="xbtn" disabled={busy} aria-label={t('recordings.add')} title={t('recordings.add')}
                                    onClick={() => add.mutate({ song, service: track.service, trackId: track.id })}><Icon name="plus" /></button>
                            )}
                        </div>
                    ))}
                </>
            )}
            <p className="note">{t('recordings.guesses')} {t(data.canAdd ? 'recordings.hereOnly' : 'recordings.inYourPlaylist')}</p>
            {signedIn && data.canAdd && (
                <p className="note">
                    <button type="button" className="lnk" disabled={find.isPending} onClick={() => find.mutate(song)}>
                        <Icon name="search" className={find.isPending ? 'spin' : undefined} />{t(find.isPending ? 'recordings.looking' : 'recordings.lookAgain')}
                    </button>
                </p>
            )}
        </>
    );
}
