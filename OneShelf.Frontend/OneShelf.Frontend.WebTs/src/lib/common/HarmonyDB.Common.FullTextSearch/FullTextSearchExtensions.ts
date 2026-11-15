import { replace, toLowerInvariant } from '../dotnet/text';

export const Separators: readonly string[] = [
    '-',
    ',',
    ' ',
    '\r',
    '\n',
    '\'',
];

export function toSearchSyntax(text: string): string {
    return replace(toLowerInvariant(text), 'ё', 'е');
}

export function searchSyntaxRemoveSeparators(text: string): string {
    return replace(text, '\'', '');
}

export function searchSyntaxAnySeparatorsToRemove(text: string): boolean {
    return text.includes('\'');
}
