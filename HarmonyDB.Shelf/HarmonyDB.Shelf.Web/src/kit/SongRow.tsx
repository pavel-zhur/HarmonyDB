// A row of a list of songs: my marks on the left, "artist — title", the sources' icons (as the device knows them for
// the person, data/labels.ts), others' marks with how many people on the right. The row one came back from (or the one
// open beside the list) is lit.

import { memo, type ReactNode } from 'react';
import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import type { Schemas } from '../api/client';
import { useMarkSign } from '../data/marks';
import { sourcesOf, useLabels } from '../data/labels';

type Source = Schemas['ChordsSource'];

/** The text with the searched part marked. */
export function Highlight({ text, q }: { text: string; q: string }) {
    const query = q.trim();
    if (!query) return <>{text}</>;
    const i = text.toLowerCase().replace(/ё/g, 'е').indexOf(query.toLowerCase().replace(/ё/g, 'е'));
    if (i < 0) return <>{text}</>;
    return <>{text.slice(0, i)}<mark>{text.slice(i, i + query.length)}</mark>{text.slice(i + query.length)}</>;
}

/** A source's icon as the server gives it for this person (its title when it has no icon). */
export function SourceIcon({ source }: { source: Source }) {
    if (source.iconUrl) return <img className="slogo" src={source.iconUrl} alt={source.title} title={source.title} loading="lazy" />;
    return <small className="muted">{source.title}</small>;
}

export interface SongRowProps {
    to: string;
    lit: boolean;
    q: string;
    title: string;
    artists: readonly string[];
    myMarks?: readonly string[];
    otherMarks?: readonly string[];
    otherPeople?: number;
    /** the sources to show; not given: those of `chordsIds`, as the device knows them */
    sources?: readonly Source[];
    /** the chords of the song's variants, in their order */
    chordsIds?: readonly string[];
    /** after the title: a playlist's name, a rating, "not available to you" */
    extra?: ReactNode;
    onOpen?: () => void;
    /** a list where no one's marks are shown (search results, a guest's): no marks column */
    noMarks?: boolean;
    /** the song's cover; with `coverSlot` (some row of the list has one), an empty square where it has none */
    cover?: string | null;
    coverSlot?: boolean;
}

export const SongRow = memo(function SongRow(p: SongRowProps) {
    const { t } = useTranslation();
    const markSign = useMarkSign();
    const mine = p.myMarks ?? [];
    const artists = p.artists.join(', ');
    const labelOf = useLabels(p.sources ? [] : p.chordsIds ?? []);
    const sources = p.sources ?? sourcesOf(labelOf, p.chordsIds ?? []);
    const more = p.sources ? 0 : Math.max(0, (p.chordsIds?.length ?? 0) - sources.length);
    return (
        <Link to={p.to} className={p.lit ? 'row last' : 'row'} onClick={p.onOpen}>
            {!p.noMarks && (
                <span className="mine">
                    {mine.slice(0, 2).map(markSign).join('')}
                    {mine.length > 2 && <small className="muted">{t('library.more', { count: mine.length - 2 })}</small>}
                </span>
            )}
            {p.cover ? <img className="rcov" src={p.cover} alt="" loading="lazy" /> : p.coverSlot ? <span className="rcov" /> : null}
            <span className="tx">
                {artists && <><Highlight text={artists} q={p.q} /> — </>}
                <b><Highlight text={p.title} q={p.q} /></b>
                {p.extra}
            </span>
            {!!sources.length && (
                <span className="lgs">
                    {sources.map(source => <SourceIcon key={source.key} source={source} />)}
                    {!!more && <small className="muted">{t('library.more', { count: more })}</small>}
                </span>
            )}
            {!!p.otherMarks?.length && (
                <span className="oth" title={t('library.others', { count: p.otherPeople ?? 0 })}>
                    {p.otherMarks.map(markSign).join('')}<i>{p.otherPeople}</i>
                </span>
            )}
        </Link>
    );
});
