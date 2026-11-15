// The original: Services/Player.cs — plays a chord through ./services-js/Player.js (imported once, under a lock).
// ChordPlayed is `event EventHandler<(int note, string fingering)?>`: handlers get (sender, { note, fingering } | null).

import type { ILogger } from '../framework/logging';
import { importJsModule, invokeJs, type PlayerJs } from '../interop/jsRuntime';
import type { ChordDataParser } from '../lib/analysis/Services/ChordDataParser';
import { DelegateEvent } from '../dotnet/events';
import { AsyncLock } from '../packages/Nito.AsyncEx/AsyncLock';

export interface PlayedChord {
    readonly note: number;
    readonly fingering: string;
}

export class Player {
    private readonly _logger: ILogger;
    private readonly _chordDataParser: ChordDataParser;

    private readonly _moduleLock = new AsyncLock();
    private _module: PlayerJs | null = null;

    readonly chordPlayed = new DelegateEvent<[sender: Player, chord: PlayedChord | null]>();

    constructor(logger: ILogger, chordDataParser: ChordDataParser) {
        this._logger = logger;
        this._chordDataParser = chordDataParser;
    }

    async playChord(chordData: string): Promise<void> {
        const parsed = this._chordDataParser.getNotes(chordData);
        if (parsed === null) return;

        if (parsed.fingering !== null) {
            this.onChordPlayed({ note: parsed.root, fingering: parsed.fingering });
        } else {
            this.onChordPlayed(null);
        }

        await this.playChordNotes(parsed.bass, parsed.main);
    }

    /** PlayChord(int[] bass, int[] main) */
    private async playChordNotes(bass: number[], main: number[]): Promise<void> {
        const _ = await this._moduleLock.lockAsync();
        try {
            this._module ??= await importJsModule<PlayerJs>('./services-js/Player.js');
        } finally {
            _.dispose();
        }

        await invokeJs<void>(this._module.playChord, bass, main);
    }

    dispose(): void {
    }

    protected onChordPlayed(chord: PlayedChord | null): void {
        this.chordPlayed.invoke(this, chord);
    }

    noChord(): void {
        this.onChordPlayed(null);
    }
}