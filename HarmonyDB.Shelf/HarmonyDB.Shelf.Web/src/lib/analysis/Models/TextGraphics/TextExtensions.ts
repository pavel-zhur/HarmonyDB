import { Text } from './Text';

/** AsText(this string text, string? css = null) and AsText(this char text, string? css = null): a char is a string of one here. */
export function asText(text: string, css: string | null = null): Text {
    const result = new Text();
    result.append(css, text);
    return result;
}
