// Shared by the page and the audio worklet.

/** Delay from the stretcher's input to its output, in seconds. The same at every ratio. */
export const LATENCY_SECONDS = 0.2;

/** The ratios the stretcher is built for: tempo 0.5–1.5 combined with ±12 semitones. */
export const MIN_RATIO = 1 / 3;
export const MAX_RATIO = 4;

export const PROCESSOR_NAME = 'shelf-tempo-stretcher';
