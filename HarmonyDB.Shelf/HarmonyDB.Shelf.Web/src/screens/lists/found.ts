// The chords a search found at the sources, as the old shelf showed them: grouped by song — the artists and the title
// in small letters, trimmed, ё as е (one without them: "n/a") — each song's versions under it in the order found. The
// old shelf's five orders: the songs in the order found, by artist, by title (as it compared them: by code units); and
// compact, each artist once with their songs, the artists in the order found or by name (their songs then by title).

import type { Schemas } from '../../api/client';

type Header = Schemas['ChordsHeader'];

export type FoundSort = 'original' | 'artist' | 'title' | 'compact' | 'compactArtist';

export const foundSorts: readonly FoundSort[] = ['original', 'artist', 'title', 'compact', 'compactArtist'];

export interface FoundSong {
    key: string;
    artist: string;
    title: string;
    versions: Header[];
}

export interface FoundArtist {
    artist: string;
    songs: FoundSong[];
}

/** The order the address names (`found=`); none or another: the order found. */
export function foundSortOf(value: string | null): FoundSort {
    return foundSorts.find(x => x === value) ?? 'original';
}

export const isCompact = (sort: FoundSort) => sort === 'compact' || sort === 'compactArtist';

const searchForm = (text: string) => text.trim().toLowerCase().replace(/ё/g, 'е');

const compare = (a: string, b: string) => (a === b ? 0 : a < b ? -1 : 1);

export function foundSongs(headers: readonly Header[], sort: FoundSort): FoundSong[] {
    const songs = new Map<string, FoundSong>();
    for (const header of headers) {
        const artist = searchForm(header.artists.join(', ')) || 'n/a';
        const title = searchForm(header.title ?? '') || 'n/a';
        const key = `${artist}\n${title}`;
        const song = songs.get(key);
        if (song) song.versions.push(header);
        else songs.set(key, { key, artist, title, versions: [header] });
    }

    const result = [...songs.values()];
    if (sort === 'artist' || sort === 'compactArtist') result.sort((a, b) => compare(a.artist, b.artist) || compare(a.title, b.title));
    if (sort === 'title') result.sort((a, b) => compare(a.title, b.title) || compare(a.artist, b.artist));
    return result;
}

/** Compact: each artist once, at their first song, with all their songs in order. */
export function foundArtists(songs: readonly FoundSong[]): FoundArtist[] {
    const artists = new Map<string, FoundArtist>();
    for (const song of songs) {
        const artist = artists.get(song.artist);
        if (artist) artist.songs.push(song);
        else artists.set(song.artist, { artist: song.artist, songs: [song] });
    }

    return [...artists.values()];
}

/** The versions one after another, as the list shows them: the order the chords' pager goes through. */
export function foundOrder(headers: readonly Header[], sort: FoundSort): string[] {
    const songs = foundSongs(headers, sort);
    return (isCompact(sort) ? foundArtists(songs).flatMap(x => x.songs) : songs).flatMap(song => song.versions.map(x => x.id));
}
