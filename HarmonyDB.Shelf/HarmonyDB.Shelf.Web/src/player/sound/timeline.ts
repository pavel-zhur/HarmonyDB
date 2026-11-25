// The player's clock: which song position is heard at which context time.
//
// A list of entries, each saying "from context time `t` the song is at `pos` and moves at `rate` song seconds per
// second". An entry holds until the next one begins. Times are those at which the sound leaves the graph, so a view or
// a scheduled sound that follows the timeline is in step with what is heard, whatever delay the tempo's stretcher adds
// inside.

export type EntryKind = 'rest' | 'play' | 'pause' | 'seek' | 'change' | 'loop' | 'end';

export interface Wrap {
    /** the sources started for the wrap (null: an end that fades the music out) */
    group: SourceGroup | null;
    old: SourceGroup[];
    xStart: number;
    xEnd: number;
}

/** Sources started together, on one deck. */
export interface SourceGroup {
    sources: AudioBufferSourceNode[];
    gains: GainNode[];
    deck: number;
    startFrame: number;
    endFrame: number | null;
    fadeFrame: number | null;
    left: number;
}

export interface Entry {
    t: number;
    pos: number;
    rate: number;
    /** the sound is not continuous across `t` (start, pause, seek, loop wrap, end) */
    cut: boolean;
    kind: EntryKind;
    wrap?: Wrap;
    announced?: boolean;
}

export class Timeline {
    entries: Entry[] = [{ t: -Infinity, pos: 0, rate: 0, cut: true, kind: 'rest' }];

    get last(): Entry {
        return this.entries[this.entries.length - 1]!;
    }

    /** The entry in force at context time `t`. */
    entryAt(t: number): Entry {
        const entries = this.entries;
        let i = entries.length - 1;
        while (i > 0 && entries[i]!.t > t) i--;
        return entries[i]!;
    }

    positionAt(t: number): number {
        const e = this.entryAt(t);
        return e.rate === 0 ? e.pos : e.pos + (t - e.t) * e.rate;
    }

    append(entry: Entry): Entry {
        if (entry.t < this.last.t) throw new Error(`timeline: entry at ${entry.t} is before the last one at ${this.last.t}`);
        this.entries.push(entry);
        return entry;
    }

    /** Removes and returns the entries that begin after `t`. */
    truncateAfter(t: number): Entry[] {
        const entries = this.entries;
        let keep = entries.length;
        while (keep > 1 && entries[keep - 1]!.t > t) keep--;
        return entries.splice(keep);
    }

    /** The first entry after `t` across which the sound is cut, if one is known. */
    nextCutAfter(t: number): Entry | null {
        for (const e of this.entries) if (e.cut && e.t > t) return e;
        return null;
    }

    /** Forgets entries that ended before `t`. */
    prune(t: number): void {
        const entries = this.entries;
        let first = 0;
        while (first + 1 < entries.length && entries[first + 1]!.t <= t) first++;
        if (first > 0) entries.splice(0, first);
    }
}
