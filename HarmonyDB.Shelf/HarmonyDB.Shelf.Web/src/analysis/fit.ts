// How chord names fit where they stand.
//
//   planLabels(names, room) → { fit, two, degrees, lefts }     the names of a bar of the grid
//     names: the bar's names in order, [{ left, width, degree }] — where each begins (its beat's place) and how wide it
//       is at its full size, px, `degree` the part of that width its degree takes
//     room: the bar's width, px
//     fit: the size the names are written at (1: their own), the same for the whole bar
//     two: they stand on two lines, above and below in turn
//     degrees: the degrees are written (false: left out — the chord's name says enough on a crowded bar)
//     lefts: where each name begins, px — a name near the bar's end ends at the bar's edge
//
// The last name is first pulled back from its beat to end at the bar's edge: that costs nothing, so a name is not
// written smaller while there is room behind it. Then the steps, each taken only when the one before does not make the
// names part: smaller, down to FIT_MIN; on two lines; without their degrees; smaller still, down to LAST_FIT — and
// there a name is pulled back from its beat as far as the next one on its line needs. What does not part even then —
// four long slash chords in a narrow bar — stands as it falls at that size.
//
//   fitName(width, degree, room) → { fit, left, degrees }        a name in its block of the time line
//     written smaller, down to FIT_MIN, to fit its block; its degree left out when the whole does not fit; a name that
//     does not fit even so begins at the block's left edge (its root is read).

const FIT_MIN = 0.7, LAST_FIT = 0.55, GAP = 5, EDGE = 6;

export interface LabelPlan { fit: number; two: boolean; degrees: boolean; lefts: number[] }

export function planLabels(names: readonly { left: number; width: number; degree: number }[], room: number): LabelPlan {
    const tried = (fit: number, two: boolean, degrees: boolean) => {
        const widths = names.map(n => (n.width - (degrees ? 0 : n.degree)) * fit);
        const lefts = names.map((n, i) => Math.max(0, Math.min(n.left, room - EDGE - widths[i]!)));
        // names that stand on one line with each other: neighbours, or every second one on two lines
        const step = two ? 2 : 1;
        const parted = names.every((_, i) => i + step >= names.length || lefts[i]! + widths[i]! + GAP <= lefts[i + step]!);
        return { fit, two, degrees, lefts, widths, parted };
    };
    // the size at which every name fits the room before the next one begins — and the last one, which may be pulled
    // back, fits with the one before it in the room from that one's place to the bar's edge
    const fitFor = (degrees: boolean) => {
        const width = (n: { width: number; degree: number }) => n.width - (degrees ? 0 : n.degree);
        const within = (fit: number, wide: number, space: number) => (wide > space ? Math.min(fit, Math.max(FIT_MIN, space / wide)) : fit);
        const last = names.length - 1;
        if (last < 0) return 1;
        const fit = names.slice(0, last).reduce((least, n, i) => within(least, width(n), names[i + 1]!.left - GAP - n.left), 1);
        return last > 0
            ? within(fit, width(names[last - 1]!) + width(names[last]!), room - EDGE - GAP - names[last - 1]!.left)
            : within(fit, width(names[0]!), room - EDGE);
    };
    const fit = fitFor(true);
    for (const plan of [tried(fit, false, true), tried(fit, true, true), tried(Math.max(FIT_MIN, fitFor(false)), true, false)]) {
        if (plan.parted) return plan;
    }
    const least = tried(LAST_FIT, true, false);
    for (let i = names.length - 3; i >= 0; i--) least.lefts[i] = Math.max(0, Math.min(least.lefts[i]!, least.lefts[i + 2]! - GAP - least.widths[i]!));
    return least;
}

export function fitName(width: number, degree: number, room: number): { fit: number; left: boolean; degrees: boolean } {
    const fit = width > room ? Math.max(FIT_MIN, room / width) : 1;
    const degrees = width * fit <= room;
    return { fit, degrees, left: (width - (degrees ? 0 : degree)) * fit > room };
}
