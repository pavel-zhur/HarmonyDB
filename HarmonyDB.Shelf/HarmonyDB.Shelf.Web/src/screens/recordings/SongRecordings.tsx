// A song's recordings, on its page: found automatically on both services and shown as what they are — guesses, each
// with its service's mark. A tap chooses the one to listen to. "Find another recording" shows the candidates: the wrong
// ones are removed, the missing ones added — in this playlist only.
//
//   <SongRecordings song={{ playlistId, songId, share }} analysed={…} analysisId={…} />
//   analysed: the song has an analysis of a recording (else "Analyse this recording" stands here, marked "soon")
//   analysisId: the analysis open on the page — its recording is one of the song's: listed (its own row, or the row of
//   the same recording on its service), and lit while it plays. A tap on a row plays that recording.
//   readOnly: only the recordings, to listen to (the public page): nothing to find, add or analyse

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
import { useAnalysis } from '../../data/analysis';
import { player } from '../../player/controller';
import { chooseAnalysis, chooseHere, useChosenRecording, usePlaysAnalysis } from './choice';
import './recordings.css';

type SongPath = { playlistId: string; songId: string; share?: string | null };

/** How many recordings are shown before "more". */
const shownFirst = 3;

type Props = { song: SongPath; analysed: boolean; analysisId?: string | null; readOnly?: boolean };

export function SongRecordings(props: Props) {
    // (read-only — the public page, always a guest — asks for no session)
    return props.readOnly ? <Recordings {...props} signedIn={false} /> : <RecordingsOfPerson {...props} />;
}

function RecordingsOfPerson(props: Props) {
    const { signedIn } = useSession();
    return <Recordings {...props} signedIn={signedIn} />;
}

function Recordings({ song, analysed, analysisId = null, readOnly = false, signedIn }: Props & { signedIn: boolean }) {
    const { t } = useTranslation();
    const recordings = useSongRecordings(song);
    const chosen = useChosenRecording(recordings.data);
    const analysis = useAnalysis(analysisId);
    const playsAnalysis = usePlaysAnalysis(song) && !!analysisId;
    const [all, setAll] = useState(false);
    const [anchor, setAnchor] = useState<HTMLElement | null>(null);
    const data = recordings.data;
    if (!data) return recordings.isError ? <p className="failed">{errorText(recordings.error)}</p> : null;

    // a tap on the recording that plays pauses it or plays on; on another, that one plays
    const choose = (itemId: string) => {
        if (lit?.itemId === itemId && !playsAnalysis) {
            player.toggle();
            return;
        }
        player.playChosen();
        chooseHere(song, itemId);
        if (signedIn && !readOnly) void chooseRecording({ song, itemId });
    };
    const toAnalysis = () => {
        if (playsAnalysis) {
            player.toggle();
            return;
        }
        player.playChosen();
        chooseAnalysis(song);
    };
    // the analysis's recording: the row of the same recording on its service, else a row of its own
    const header = analysis.data?.header, recording = analysis.data?.content?.recording;
    const analysedTrack = recording?.audio.track ?? null;
    const sameRow = analysedTrack ? data.recordings.find(r => r.track.service === analysedTrack.service && r.track.id === analysedTrack.id) ?? null : null;
    const ownRow = !!analysisId && !!header && !!recording && !sameRow;
    const lit = playsAnalysis ? sameRow : chosen;
    const list = data.recordings;
    const shown = all ? list : list.slice(0, shownFirst);
    if (lit && !shown.includes(lit)) shown.push(lit);

    return (
        <div className="rec-list">
            <div className="rec-lab">{t('recordings.title')}</div>
            {list.length === 0 && !ownRow && <p className="muted small">{t('recordings.none')}</p>}
            {ownRow && (
                <button type="button" className={playsAnalysis ? 'rec-row on' : 'rec-row'} aria-pressed={playsAnalysis} onClick={toAnalysis}>
                    <span className="rec-cov rec-an"><Icon name="ai" /></span>
                    <span className="rec-t1">
                        <b>{header!.title}</b>
                        <span>{[header!.artists.join(', '), t('player.analysisRecording')].filter(Boolean).join(' · ')}</span>
                    </span>
                    <span className="d">{clock(recording!.duration)}</span>
                </button>
            )}
            {shown.map(r => (
                <button type="button" key={r.itemId} className={r === lit ? 'rec-row on' : 'rec-row'} aria-pressed={r === lit}
                    onClick={() => (r === sameRow ? toAnalysis() : choose(r.itemId))}>
                    <RecordingLine track={r.track} analysed={r === sameRow} />
                </button>
            ))}
            {!readOnly && <div className="links">
                {list.length > shown.length && <button type="button" className="lnk" onClick={() => setAll(true)}>{t('recordings.more', { count: list.length - shown.length })}</button>}
                <button type="button" className="lnk" onClick={e => setAnchor(anchor ? null : e.currentTarget)}>{t('recordings.findAnother')}</button>
                {!analysed && chosen && <span className="lnk off"><Icon name="ai" />{t('music.analyse')} <Soon /></span>}
            </div>}
            {!readOnly && (
                <Popover anchor={anchor} onClose={() => setAnchor(null)} width={380} label={t('recordings.findAnother')}>
                    <FindAnother song={song} data={data} />
                </Popover>
            )}
        </div>
    );
}

function RecordingLine({ track, analysed = false }: { track: MusicTrack; analysed?: boolean }) {
    const { t } = useTranslation();
    const sub = [artistNames(track.artists), track.version, track.album?.title, analysed ? t('player.analysisRecording') : null].filter(Boolean).join(' · ');
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
