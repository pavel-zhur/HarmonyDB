/** TranspositionExtensions.Transposition(this int) (and the byte overload): "+3", "0", "−3" (U+2212). */
export function transposition(transposition: number): string {
    return transposition > 0
        ? `+${transposition}`
        : transposition === 0
            ? '0'
            : transposition.toString().replace('-', '−');
}
