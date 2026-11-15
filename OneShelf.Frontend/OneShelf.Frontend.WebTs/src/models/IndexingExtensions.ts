// The original: Models/IndexingExtensions.cs.

import { ArgumentOutOfRangeException } from '../lib/common/dotnet/exceptions';

/** `index.ToLetterIndex()` */
export function toLetterIndex(index: number): string {
    const letters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';

    if (index >= letters.length * (letters.length + 1)) {
        return index.toString();
    }

    if (index < 0) throw new ArgumentOutOfRangeException('index', `Negatives are not supported. (Parameter 'index')\nActual value was ${index}.`);

    const a = index % letters.length;
    const b = Math.trunc(index / letters.length);

    return b === 0 ? letters[a]! : `${letters[b - 1]}${letters[a]}`;
}