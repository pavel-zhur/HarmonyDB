// What the sound's users need without loading the sound itself (player.ts is loaded when a recording is opened):
// its limits, its errors, the shape of an instrument.

export const MIN_TEMPO = 0.5;
export const MAX_TEMPO = 1.5;
export const MIN_LOOP_SECONDS = 0.1;

export type PlayerErrorCode = 'gesture-required' | 'no-output' | 'no-song' | 'bad-sources' | 'underrun' | 'busy';

export class PlayerError extends Error {
    constructor(readonly code: PlayerErrorCode, message: string) {
        super(message);
        this.name = 'PlayerError';
    }
}

/** A separate instrument of the recording: its name and its sound, sample-aligned with the recording. */
export interface Stem { name: string; buffer: AudioBuffer }
