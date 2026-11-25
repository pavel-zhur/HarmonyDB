// A track of a music service: to listen to (the player at the bottom, its clip), to put into a playlist as a song made of
// this one recording, to go on from — its artists, album, similar tracks, radio; its words. Analysing it is "soon".

import { useCallback, useRef, useState } from 'react';
import { Link } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ToTop } from '../../app/ToTop';
import { createPlaylist, playlistTitle, useMyPlaylists, type PlaylistSummary } from '../../data/playlists';
import { useMusicLyrics, useMusicTrack, type MusicTrack } from '../../data/music';
import { addRecordingSong } from '../../data/recordings';
import { useSession } from '../../data/session';
import { offerSignIn } from '../../data/signInOffer';
import { Soon } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import { MenuItem, MenuTitle, Popover } from '../../kit/Popover';
import { ClipBox, PlayerBar } from '../../player/PlayerBar';
import { player, useClock } from '../../player/controller';
import { Back, Loaded, ServiceMark, clock, musicHref, type MusicPageProps } from './parts';

export function TrackScreen({ service, id }: MusicPageProps) {
    const track = useMusicTrack({ service, id });
    const source = track.data ? { kind: 'track' as const, track: track.data.track } : null;
    return (
        <div className="pane">
            <div className="pad">
                <Back />
                <Loaded query={track}>{page => <TrackBody track={page.track} lyrics={page.lyricsAvailable} />}</Loaded>
            </div>
            <PlayerBar source={source} />
            <ToTop />
        </div>
    );
}

function TrackBody({ track, lyrics }: { track: MusicTrack; lyrics: boolean }) {
    const { t } = useTranslation();
    const service = track.service;
    return (
        <>
            <div className="mu-mhero">
                {track.coverUrl ? <img className="alc" src={track.coverUrl} alt="" referrerPolicy="no-referrer" /> : <span className="alc mu-blank" />}
                <div>
                    <div className="kind">{t(track.isVideo ? 'music.video' : 'music.recording')} · <ServiceMark service={service} />{track.durationMs ? ` · ${clock(track.durationMs / 1000)}` : ''}</div>
                    <h1>{track.title}{track.version ? <span className="muted"> · {track.version}</span> : null}</h1>
                    <div className="who">
                        {track.artists.map((artist, i) => (
                            <span key={artist.id ?? i}>
                                {i > 0 && ', '}
                                {artist.id ? <Link to={musicHref.artist(service, artist.id)}>{artist.name}</Link> : artist.name}
                            </span>
                        ))}
                        {track.album?.title && (
                            <> · {track.album.id ? <Link to={musicHref.album(service, track.album.id)}>{track.album.title}</Link> : track.album.title}</>
                        )}
                    </div>
                </div>
            </div>
            {track.available === false && <p className="muted small">{t('music.unavailable')}</p>}
            <div className="mu-mactions">
                <AddToPlaylist track={track} />
                <button type="button" className="btn" disabled><Icon name="ai" />{t('music.analyse')} <Soon /></button>
            </div>
            <div className="mu-mlinks">
                {track.inMyPlaylists && <span className="lnk muted"><Icon name="pls" />{t('music.inMyPlaylists')}</span>}
                {track.analysisChords && <Link className="lnk" to={`/chords/${encodeURIComponent(track.analysisChords)}`}><Icon name="ai" />{t('music.openAnalysis')}</Link>}
                <Link className="lnk" to={musicHref.similar(service, track.id)}><Icon name="simil" />{t('music.similar')}</Link>
                <Link className="lnk" to={musicHref.radio(service, track.id)}><Icon name="loop" />{t('music.radio')}</Link>
            </div>
            <ClipBox />
            {lyrics && <Lyrics track={track} />}
        </>
    );
}

/** Puts the track into one of my playlists as a song made of this one recording. */
function AddToPlaylist({ track }: { track: MusicTrack }) {
    const { t } = useTranslation();
    const { signedIn } = useSession();
    const playlists = useMyPlaylists();
    const add = useMutation({ mutationFn: addRecordingSong });
    const [anchor, setAnchor] = useState<HTMLElement | null>(null);
    const mine = (playlists.data ?? []).filter(canAddTo);
    return (
        <>
            <button type="button" className="btn pri" disabled={add.isPending}
                onClick={e => (signedIn ? setAnchor(anchor ? null : e.currentTarget) : offerSignIn('change'))}>
                <Icon name="addpl" />{t('music.addToPlaylist')}
            </button>
            <Popover anchor={anchor} onClose={() => setAnchor(null)} label={t('music.addToPlaylist')}>
                <MenuTitle>{t('music.addToPlaylistTitle')}</MenuTitle>
                {mine.map(p => (
                    <MenuItem key={p.id} onClick={() => {
                        setAnchor(null);
                        add.mutate({ playlistId: p.id, service: track.service, trackId: track.id });
                    }}>{playlistTitle(p)}</MenuItem>
                ))}
                {!mine.length && !playlists.isPending && (
                    <>
                        <p className="note">{t('music.noPlaylistToAdd')}</p>
                        <NewPlaylistWith onMade={playlistId => {
                            setAnchor(null);
                            add.mutate({ playlistId, service: track.service, trackId: track.id });
                        }} />
                    </>
                )}
                <p className="note">{t('music.addNote')}</p>
            </Popover>
        </>
    );
}

/** A playlist made here, the recording put into it at once. */
function NewPlaylistWith({ onMade }: { onMade: (playlistId: string) => void }) {
    const { t } = useTranslation();
    const [title, setTitle] = useState('');
    const create = useMutation({ mutationFn: createPlaylist, onSuccess: r => onMade(r.value.playlist.id) });
    return (
        <form className="inline-form" onSubmit={e => { e.preventDefault(); if (title.trim()) create.mutate(title.trim()); }}>
            <input className="field" value={title} onChange={e => setTitle(e.target.value)} placeholder={t('playlists.newPlaceholder')} aria-label={t('playlists.newPlaceholder')} />
            <button type="submit" className="btn" disabled={!title.trim() || create.isPending}><Icon name="plus" />{t('common.create')}</button>
        </form>
    );
}

function canAddTo(p: PlaylistSummary): boolean {
    if (p.systemKind !== 'none') return false;
    return p.myRole === 'owner' || p.myRole === 'admin' || (p.myRole === 'member' && p.addPolicy === 'allMembers');
}

/** The track's words; timed ones light the line that is sung and go there on a tap. */
function Lyrics({ track }: { track: MusicTrack }) {
    const { t } = useTranslation();
    const lyrics = useMusicLyrics({ service: track.service, id: track.id }, true);
    const box = useRef<HTMLDivElement>(null);
    const lit = useRef(-1);
    const onTime = useCallback((position: number) => {
        const lines = lyrics.data?.synced ? lyrics.data.lines : null;
        if (!lines || !box.current) return;
        const ms = position * 1000;
        let now = -1;
        lines.forEach((line, i) => { if (line.startMs !== null && line.startMs !== undefined && line.startMs <= ms) now = i; });
        if (now === lit.current) return;
        box.current.children[lit.current]?.classList.remove('now');
        box.current.children[now]?.classList.add('now');
        lit.current = now;
    }, [lyrics.data]);
    useClock(onTime);
    return (
        <>
            <h2>{t('music.lyrics')}</h2>
            <Loaded query={lyrics}>
                {data => (data.available && data.lines.length ? (
                    <div className="mu-mlyrics">
                        <div ref={box}>
                            {data.lines.map((line, i) => (data.synced && line.startMs !== null && line.startMs !== undefined
                                ? <p key={i} className="timed" onClick={() => player.playFrom(line.startMs! / 1000)}>{line.text || ' '}</p>
                                : <p key={i}>{line.text || ' '}</p>))}
                        </div>
                        {!!data.writers?.length && <small>{t('music.writers', { names: data.writers.join(', ') })}</small>}
                    </div>
                ) : <p className="empty">{t('music.noLyrics')}</p>)}
            </Loaded>
        </>
    );
}
