import { InvalidOperationException } from '../../dotnet/exceptions';
import { isNullOrWhiteSpace, split, StringSplitOptions, toLowerInvariant, trim, tryParseInt32 } from '../../dotnet/text';
import { searchSyntaxRemoveSeparators, Separators, toSearchSyntax } from '../../HarmonyDB.Common.FullTextSearch/FullTextSearchExtensions';
import { getRating } from '../SongsConstants';
import type { ISearchableArtist } from './ISearchableArtist';
import type { ISearchableSong } from './ISearchableSong';

export type FullTextSearchCache<T> = readonly { readonly song: T; readonly words: ReadonlySet<string> }[];

/** Comparer<T?>.Default for numbers: null first. Descending sorts reverse it. */
function compareNullable(a: number | null, b: number | null): number {
    if (a === null) return b === null ? 0 : -1;
    if (b === null) return 1;
    return a < b ? -1 : a > b ? 1 : 0;
}

export function find<T extends ISearchableSong>(
    cache: FullTextSearchCache<T>,
    query: string,
    userId: number,
): { found: T[]; isUserSpecific: boolean } {
    const terms = [...new Set(
        split(toSearchSyntax(toLowerInvariant(query)), Separators, StringSplitOptions.RemoveEmptyEntries)
            .map(trim)
            .filter(x => !isNullOrWhiteSpace(x)))];

    if (terms.length === 0) {
        const items = cache.map(x => {
            let level: number | null = null;
            for (const like of x.song.likes) {
                if (like.userId === userId && (level === null || like.level > level)) level = like.level;
            }
            return { song: x.song, level, rating: getRating(x.song.likes) };
        });
        // OrderByDescending(level).ThenByDescending(rating).ThenByDescending(Index) — a stable sort
        items.sort((a, b) =>
            compareNullable(b.level, a.level)
            || compareNullable(b.rating, a.rating)
            || compareNullable(b.song.index, a.song.index));
        return { found: items.map(x => x.song), isUserSpecific: true };
    }

    const found: { song: T; fullyFound: number; beginningFound: number }[] = [];
    for (const { song, words } of cache) {
        let fullyFound = 0;
        let beginningFound = 0;
        let songMatches = true;

        for (const term of terms) {
            if (words.has(term)) {
                fullyFound++;
                continue;
            }

            let termMatches = false;
            for (const word of words) {
                if (word.startsWith(term)) {
                    beginningFound++;
                    termMatches = true;
                    break;
                }

                if (word.includes(term)) {
                    termMatches = true;
                    break;
                }
            }

            if (!termMatches) {
                songMatches = false;
                break;
            }
        }

        if (songMatches) found.push({ song, fullyFound, beginningFound });
    }

    const distinctUsers = (song: T) => {
        const users = new Set<number>();
        for (const like of song.likes) users.add(like.userId);
        return users.size;
    };

    const foundSongs = found
        .map(x => ({ ...x, users: distinctUsers(x.song) }))
        .sort((a, b) =>
            compareNullable(b.fullyFound, a.fullyFound)
            || compareNullable(b.beginningFound, a.beginningFound)
            || compareNullable(b.users, a.users)
            || compareNullable(b.song.index, a.song.index))
        .map(x => x.song);

    let foundByIndex: T | null = null;
    const value = tryParseInt32(trim(query));
    if (value !== null) {
        // SingleOrDefault(x => x.song.Index == value).song
        const matching = cache.filter(x => x.song.index === value);
        if (matching.length > 1) throw new InvalidOperationException('Sequence contains more than one matching element');
        foundByIndex = matching[0]?.song ?? null;
    }

    return {
        found: foundByIndex != null ? [foundByIndex, ...foundSongs] : foundSongs,
        isUserSpecific: false,
    };
}

export function buildCache<T extends ISearchableSong>(
    songs: Iterable<T>,
    artists: (song: T) => Iterable<ISearchableArtist>,
): FullTextSearchCache<T> {
    const result: { song: T; words: Set<string> }[] = [];
    for (const x of songs) {
        const texts: (string | null)[] = [];
        for (const a of artists(x)) {
            texts.push(a.name);
            for (const synonym of a.synonyms) texts.push(synonym);
        }
        texts.push(x.title);
        texts.push(x.additionalKeywords);

        const words = new Set<string>();
        for (const text of texts) {
            const removed = text == null ? null : searchSyntaxRemoveSeparators(text);
            if (isNullOrWhiteSpace(removed)) continue;
            for (const word of split(toSearchSyntax(removed!), Separators, StringSplitOptions.RemoveEmptyEntries)) {
                const trimmed = trim(word);
                if (!isNullOrWhiteSpace(trimmed)) words.add(trimmed);
            }
        }

        result.push({ song: x, words });
    }
    return result;
}
