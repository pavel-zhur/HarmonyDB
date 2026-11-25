// The pure part of the device's copy: the keys its answers are kept under, what belongs to the library (the songs and
// the chords the copy keeps), and a list's page answered from the rows kept — searched, filtered and sorted here as
// near to the server as the rows allow.

import type { Schemas } from '../api/client';

type SongRow = Schemas['SongRow'];
type SongList = Schemas['SongList'];

export const keys = {
    /** the library's rows, all of them, in the server's first order */
    library: 'list:library',
    /** a playlist's rows, all of them, in its order */
    playlistRows: (id: string) => `list:playlist:${id}`,
    myPlaylists: 'playlists:mine',
    playlist: (id: string) => `playlist:${id}`,
    song: (playlistId: string, songId: string) => `song:${playlistId}/${songId}`,
    chords: (id: string) => `chords:${id}`,
};

/** What the library holds: its songs (by key) and its chords (by id), from its rows and its playlists' rows. */
export interface Membership {
    songs: Set<string>;
    chords: Set<string>;
}

export function membership(lists: readonly (readonly SongRow[])[]): Membership {
    const songs = new Set<string>(), chords = new Set<string>();
    for (const rows of lists) {
        for (const row of rows) {
            if (row.songId && row.playlistId) songs.add(keys.song(row.playlistId, row.songId));
            for (const id of row.chordsIds) chords.add(id);
            if (row.chords) chords.add(row.chords.id);
        }
    }
    return { songs, chords };
}

/** A list kept whole: its rows and the people they name. */
export interface KeptList {
    rows: SongRow[];
    people: Schemas['PersonRef'][];
}

/** The pages of a list, read to its end, kept as one. */
export function joinPages(pages: readonly SongList[]): KeptList {
    const people = new Map<string, Schemas['PersonRef']>();
    for (const page of pages) for (const person of page.people) people.set(person.id, person);
    return { rows: pages.flatMap(page => page.rows), people: [...people.values()] };
}

export interface ListQuery {
    q?: string;
    sort?: string;
    who?: string;
    mark?: string;
    offset?: number;
    limit?: number;
}

const folded = (text: string) => text.toLocaleLowerCase().replace(/ё/g, 'е');

/**
 * A page of a list from the rows kept; null when the copy cannot answer it. The search looks in the titles and the
 * artists (the server's looks in the words too: the copy has no words of the rows). A mark filters by my marks; another
 * person's marks are not in the rows (only everyone's together), so a filter by a person is not answered. The sorts by
 * title and by artist are done here, the others keep the order the rows were kept in.
 */
export function answerList(kept: KeptList, query: ListQuery): SongList | null {
    const person = query.who && query.who !== 'me' && query.who !== 'all';
    if (person) return null;
    let found = kept.rows.slice();
    const q = folded(query.q?.trim() ?? '');
    if (q) found = found.filter(row => folded(`${row.artists.join(' ')} ${row.title}`).includes(q));
    if (query.mark) found = found.filter(row => (query.who === 'all' ? [...row.myMarks, ...row.otherMarks] : row.myMarks).includes(query.mark!));
    const by = (text: (row: SongRow) => string) => found.sort((a, b) => text(a).localeCompare(text(b)));
    if (query.sort === 'title') by(row => row.title);
    if (query.sort === 'artist') by(row => `${row.artists.join(', ')} ${row.title}`);
    const offset = query.offset ?? 0;
    return { total: found.length, offset, rows: found.slice(offset, offset + (query.limit ?? found.length)), people: kept.people };
}
