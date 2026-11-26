// The own chords' editor, with the preview beside the text (below it on a phone):
//   /editor                              a new song (into "My chords", and the playlists ticked)
//   /editor?playlist=<id>&song=<id>      my variant of a song of a playlist (its title and artists are the song's)
//   /editor?own=<id>                     my own chords, a new version of them
//   …&from=<chordsId>                    the text starts from those chords (a copy of an analysis, of another variant)
// The markup: a line beginning with ">" is a line of chords over the next one; a chord in "<" and ">" stands in the
// line; a line in "[" and "]" is a heading.

import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { Page } from '../../app/panes';
import { formatAgo, formatDate } from '../../i18n';
import { useSession } from '../../data/session';
import { forgetDraft, readDraft, writeDraft, type Draft } from './draft';
import { createOwnChords, updateOwnChords, useOwnChordsDraft, useOwnChordsItem, useOwnChordsPreview, type OwnChordsText } from '../../data/ownChords';
import { playlistTitle, useMyPlaylists } from '../../data/playlists';
import { useChordsContent, useSongPage } from '../../data/song';
import { LinkBox } from '../../kit/LinkBox';
import { sheetFromApi } from '../../chords/sheet';
import { ChordSheet } from '../../chords/ChordSheet';
import { RepresentationSettings } from '../../lib/representations';
import { Confirm, Seg } from '../../kit/controls';
import { Icon } from '../../kit/Icon';
import './editor.css';

type Visibility = OwnChordsText['visibility'];

export function EditorScreen() {
    const [params] = useSearchParams();
    const own = params.get('own');
    const playlistId = params.get('playlist');
    const songId = params.get('song');
    const from = params.get('from');
    const key = [own, playlistId, songId, from].join('|');
    return (
        <Page wide>
            {own ? <OwnLoader key={key} id={own} />
                : playlistId && songId ? <SongLoader key={key} playlistId={playlistId} songId={songId} from={from} />
                    : <Loaded key={key} failed={null} ready from={from} />}
        </Page>
    );
}

function OwnLoader({ id }: { id: string }) {
    const { t } = useTranslation();
    const item = useOwnChordsItem(id);
    const data = item.data?.item;
    // not editable here (the store's word): changed where they were written
    if (data && !data.editable) return <p className="muted">{t('song.menu.notEditable')}</p>;
    return <Loaded failed={item.error} ready={!!data} from={null} existing={data ? { id, chordsId: data.chordsId ?? null, text: data.text, version: data.version, updatedAt: data.updatedAt } : null} />;
}

function SongLoader({ playlistId, songId, from }: { playlistId: string; songId: string; from: string | null }) {
    const song = useSongPage(playlistId, songId, null);
    const page = song.data;
    return <Loaded failed={song.error} ready={!!page} from={from} song={page ? { playlistId, songId, title: page.song.title, artists: page.song.artists } : null} />;
}

/** The rest of what the editor starts from: the text of other chords, when asked. */
function Loaded({ failed, ready, from, existing = null, song = null }: {
    failed: unknown; ready: boolean; from: string | null; existing?: Existing | null; song?: SongOf | null;
}) {
    const { t } = useTranslation();
    const draft = useOwnChordsDraft(from);
    const error = failed ?? draft.error;
    if (error) return <p className="failed">{errorText(error)}</p>;
    if (!ready || (from && !draft.data)) return <p className="loading">{t('common.loading')}</p>;
    return <Editor existing={existing} song={song} start={draft.data ?? null} />;
}

type Existing = { id: string; chordsId: string | null; text: OwnChordsText; version: number; updatedAt: string };
type SongOf = { playlistId: string; songId: string; title: string; artists: string[] };

function Editor({ existing, song, start }: { existing: Existing | null; song: SongOf | null; start: OwnChordsText | null }) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { accountId } = useSession();
    const target = existing ? `own:${existing.id}` : song ? `variant:${song.playlistId}/${song.songId}` : 'new';
    const first = existing?.text ?? start;
    const fresh: Draft = {
        title: song?.title ?? first?.title ?? '',
        artists: (song?.artists ?? first?.artists ?? []).join(', '),
        contents: first?.contents ?? t('editor.sample'),
        description: existing?.text.description ?? '',
        key: existing?.text.key ?? start?.key ?? '',
        bpm: existing?.text.bpm?.toString() ?? start?.bpm?.toString() ?? '',
        visibility: existing?.text.visibility ?? 'link',
        comment: '',
        also: [],
        at: 0,
    };
    // what was typed before and not saved comes back (the song's own title and artists stay the song's)
    const [kept] = useState(() => (accountId ? readDraft(accountId, target) : null));
    const [form, setForm] = useState<Draft>(() => (kept ? { ...kept, ...(song ? { title: fresh.title, artists: fresh.artists } : {}) } : fresh));
    const [dirty, setDirty] = useState(!!kept);
    const [asking, setAsking] = useState(false);
    const edit = (patch: Partial<Draft>) => {
        setForm(f => ({ ...f, ...patch }));
        setDirty(true);
        // a new change: the next saving is to come (and says again what it does to the link)
        if (update.isSuccess) update.reset();
    };
    // every keystroke is kept at once: leaving the page, or reloading it, right after typing loses nothing
    useEffect(() => {
        if (dirty && accountId) writeDraft(accountId, target, { ...form, at: Date.now() });
    }, [form, dirty, accountId, target]);
    const discard = () => {
        if (accountId) forgetDraft(accountId, target);
        setForm(fresh);
        setDirty(false);
    };
    const { title, artists, contents, description, key: keyName, bpm, visibility, comment, also } = form;
    const setTitle = (title: string) => edit({ title });
    const setArtists = (artists: string) => edit({ artists });
    const setContents = (contents: string) => edit({ contents });
    const setDescription = (description: string) => edit({ description });
    const setKeyName = (key: string) => edit({ key });
    const setBpm = (bpm: string) => edit({ bpm });
    const setVisibility = (visibility: Visibility) => edit({ visibility });
    const setComment = (comment: string) => edit({ comment });
    const setAlso = (also: string[]) => edit({ also });
    const saved = () => {
        if (accountId) forgetDraft(accountId, target);
        setDirty(false);
    };
    const playlists = useMyPlaylists();
    // where else new chords may go: my playlists I may add to (an admin's; a member's where all members add), not the
    // system ones
    const others = (playlists.data ?? []).filter(p => p.systemKind === 'none' && p.id !== song?.playlistId
        && (p.myRole === 'owner' || p.myRole === 'admin' || (p.myRole === 'member' && p.addPolicy === 'allMembers')));

    const text = (): OwnChordsText => ({
        title: title.trim(),
        artists: artists.split(',').map(x => x.trim()).filter(Boolean),
        contents,
        description: description.trim() || null,
        key: keyName.trim() || null,
        bpm: bpm.trim() ? Number(bpm) : null,
        visibility,
        versionComment: existing ? comment.trim() || null : null,
    });

    const create = useMutation({
        mutationFn: createOwnChords,
        onSuccess: r => {
            saved();
            if (song) navigate(`/playlists/${song.playlistId}/songs/${song.songId}`);
            else if (r.item.chordsId) navigate(`/chords/${encodeURIComponent(r.item.chordsId)}`);
            else navigate(`/editor?own=${r.item.id}`, { replace: true });
        },
    });
    const update = useMutation({ mutationFn: updateOwnChords, onSuccess: saved });
    // kept to their link: each saving gives them a new link, and the one given out before stops opening
    const byLink = existing?.text.visibility === 'private';
    const save = () => {
        if (existing) update.mutate({ id: existing.id, text: text() });
        else create.mutate({ text: text(), playlistId: song?.playlistId ?? null, songId: song?.songId ?? null, alsoInPlaylists: also });
    };
    const busy = create.isPending || update.isPending;

    return (
        <div className="form ed-form">
            <h1>{existing ? t('editor.editTitle', { title: existing.text.title }) : song ? t('editor.variantTitle', { title: song.title }) : t('editor.newTitle')}</h1>
            {existing && <p className="muted small ed-ver">{t('editor.version', { n: existing.version, date: formatDate(existing.updatedAt) })}</p>}
            {dirty && (asking
                ? <Confirm text={t('editor.discardAsk')} choices={[[t('editor.discardYes'), () => { setAsking(false); discard(); }]]} onCancel={() => setAsking(false)} />
                : (
                    <p className="ed-draft muted small">
                        {kept && form.at === kept.at ? t('editor.draftBack', { ago: formatAgo(new Date(kept.at).toISOString()) }) : t('editor.draftKept')}{' '}
                        <button type="button" className="lnk" onClick={() => setAsking(true)}>{t('editor.discard')}</button>
                    </p>
                ))}
            <div className="ed-grid">
                <label><span className="lab">{t('editor.title')}</span>
                    <input className="field" value={title} disabled={!!song} maxLength={200} onChange={e => setTitle(e.target.value)} placeholder={t('editor.titlePlaceholder')} /></label>
                <label><span className="lab">{t('editor.artists')}</span>
                    <input className="field" value={artists} disabled={!!song} onChange={e => setArtists(e.target.value)} placeholder={t('editor.artistsPlaceholder')} /></label>
            </div>
            {song && <p className="muted small ed-ver">{t('editor.variantNote')}</p>}

            {!existing && (
                <section>
                    <span className="lab">{t('editor.also')}</span>
                    {others.length > 0 && (
                        <div className="chips">
                            {others.map(p => (
                                <button type="button" key={p.id} className={also.includes(p.id) ? 'chip on' : 'chip'} aria-pressed={also.includes(p.id)}
                                    onClick={() => setAlso(also.includes(p.id) ? also.filter(x => x !== p.id) : [...also, p.id])}>
                                    {also.includes(p.id) && <Icon name="check" />}{playlistTitle(p)}
                                </button>
                            ))}
                        </div>
                    )}
                    <p className="muted small ed-ver">{t('editor.alsoNote')}</p>
                </section>
            )}

            <div className="ed-grid">
                <div><span className="lab">{t('editor.write')}</span>
                    <textarea className="field ed-text" value={contents} spellCheck={false} onChange={e => setContents(e.target.value)} aria-label={t('editor.write')} /></div>
                <div><span className="lab">{t('editor.preview')}</span><Preview contents={contents} /></div>
            </div>

            <section>
                <span className="lab">{t('editor.more')}</span>
                <div className="lines">
                    <div className="setrow"><span>{t('editor.description')}<span className="sub">{t('editor.descriptionSub')}</span></span></div>
                    <textarea className="field ed-desc" value={description} onChange={e => setDescription(e.target.value)} aria-label={t('editor.description')} />
                    <div className="setrow">
                        <span>{t('editor.key')}</span>
                        <input className="field ed-small" value={keyName} onChange={e => setKeyName(e.target.value)} placeholder="Am" aria-label={t('editor.key')} />
                    </div>
                    <div className="setrow">
                        <span>{t('editor.bpm')}</span>
                        <input className="field ed-small" value={bpm} inputMode="numeric" onChange={e => setBpm(e.target.value.replace(/\D/g, ''))} placeholder="120" aria-label={t('editor.bpm')} />
                    </div>
                    <div className="setrow">
                        <span>{t('editor.who')}<span className="sub">{t(`editor.whoSub.${visibility}`)}</span></span>
                        <Seg value={visibility} options={(['private', 'link', 'public'] as const).map(v => [v, t(`editor.whoChoice.${v}`)])} onChange={setVisibility} />
                    </div>
                    {existing && (
                        <div className="setrow">
                            <span>{t('editor.comment')}</span>
                            <input className="field ed-wide" value={comment} onChange={e => setComment(e.target.value)} placeholder={t('editor.commentPlaceholder')} aria-label={t('editor.comment')} />
                        </div>
                    )}
                </div>
            </section>

            {(create.isError || update.isError) && <p className="failed">{errorText(create.error ?? update.error)}</p>}
            {byLink && !update.isSuccess && <p className="muted small">{t('editor.newLink')}</p>}
            <div><button type="button" className="btn pri" disabled={busy || !title.trim() || !contents.trim()} onClick={save}><Icon name="check" />{t('editor.save')}</button></div>
            {byLink && update.isSuccess && existing?.chordsId && <NewLink chordsId={existing.chordsId} />}
        </div>
    );
}

/** After saving chords kept to their link: their new link, to give out in place of the old one. */
function NewLink({ chordsId }: { chordsId: string }) {
    const { t } = useTranslation();
    const address = useChordsContent(chordsId).data?.header?.address ?? null;
    return (
        <section>
            <p className="muted small">{t('editor.newLinkDone')}</p>
            {address ? <LinkBox link={address} /> : <p className="loading">{t('common.loading')}</p>}
        </section>
    );
}

/** How the text will look, a moment after typing stops. */
function Preview({ contents }: { contents: string }) {
    const { t } = useTranslation();
    const [settled, setSettled] = useState(contents);
    useEffect(() => {
        const timer = setTimeout(() => setSettled(contents), 500);
        return () => clearTimeout(timer);
    }, [contents]);
    const preview = useOwnChordsPreview(settled);
    const sheet = useMemo(() => (preview.data?.sheet ? sheetFromApi(preview.data.sheet.root) : null), [preview.data]);
    const settings = useMemo(() => new RepresentationSettings({ isVariableWidth: sheet?.isVariableWidth ?? false }), [sheet]);
    return (
        <div className="ed-prev">
            {preview.isError && <p className="failed">{errorText(preview.error)}</p>}
            {sheet && <div className="sheetbox"><ChordSheet sheet={sheet} settings={settings} /></div>}
            {!sheet && !preview.isError && settled.trim() && <p className="loading">{t('common.loading')}</p>}
        </div>
    );
}
