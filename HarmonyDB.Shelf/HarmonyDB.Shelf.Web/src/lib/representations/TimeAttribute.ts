/**
 * internal: the time of a chord, a word, a line or a mark in the rendered HTML: `data-time`, in seconds from the start,
 * so that a player can show what sounds now and go to the place that is tapped.
 */
export const TimeAttribute = {
    format: (seconds: number): string => `data-time='${String(seconds)}'`,
};
