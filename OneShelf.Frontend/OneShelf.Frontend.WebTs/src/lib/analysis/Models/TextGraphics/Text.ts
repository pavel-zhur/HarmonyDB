import { toChunks, withIsFirst } from '../../../common/OneShelf.Common/Extensions';
import { asText } from './TextExtensions';

/** Environment.NewLine in the browser (WebAssembly) and on Linux. */
const environmentNewLine = '\n';

export class Text {
    private _pieces: { css: string | null; text: string }[] = [];

    static readonly CssTextLightGray = 'text-lightgray';
    static readonly CssTextLightYellow = 'text-lightyellow';
    static readonly CssTextRed = 'text-danger';

    append(css: string | null, text: string): void {
        const last = this._pieces[this._pieces.length - 1];
        if (this._pieces.length > 0 && last !== undefined && last.css === css) {
            this._pieces[this._pieces.length - 1] = { ...last, text: last.text + text };
        } else {
            this._pieces.push({ css, text });
        }
    }

    /** Append(Text text): adds the pieces as they are, without merging them with the last piece. */
    appendText(text: Text): void {
        text._pieces.forEach(x => this._pieces.push(x));
    }

    static get Empty(): Text {
        return new Text();
    }

    static get EmptyLineContent(): Text {
        return asText(' ');
    }

    static get NewLine(): Text {
        return asText(environmentNewLine);
    }

    static join(separator: Text, lines: Iterable<Text>): Text {
        const result = Text.Empty;
        for (const { x, isFirst } of withIsFirst(lines)) {
            if (!isFirst) {
                result.appendText(separator);
            }

            result.appendText(x);
        }

        return result;
    }

    asHtml(): string {
        this._pieces = toChunks(this._pieces, x => x.css).map(c => ({ css: c.criterium, text: c.chunk.map(x => x.text).join('') }));

        let result = '';
        for (const { css, text } of this._pieces) {
            result += `<span class="${css ?? ''}">${text}</span>`;
        }

        return result;
    }

    toString(): string {
        let result = '';
        for (const { text } of this._pieces) {
            result += text;
        }

        return result;
    }
}
