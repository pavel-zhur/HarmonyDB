import { InvalidOperationException } from '../common/dotnet/exceptions';
import { htmlAttributeEncode, htmlEncode } from '../common/dotnet/html';
import { jsonClass, jsonDouble, jsonNullable, jsonProperty, type JsonContract } from '../common/dotnet/json/JsonSerializer';
import { StringBuilder } from '../common/dotnet/StringBuilder';
import type { RepresentationSettings } from './RepresentationSettings';
import { SheetKey } from './SheetKey';
import { SheetTempo } from './SheetTempo';
import { SheetTimeSignature } from './SheetTimeSignature';
import { TimeAttribute } from './TimeAttribute';
import type { TraversalContext } from './TraversalContext';
import { NodeBase, type Ref } from './NodeBase';
import './internal'; // fills nodeTypes, which the base class uses

// The drawings are in staff spaces of 10 units; the staff's top line is at 0, its bottom line at 40.
const Space = 10;

// The places of the signs of a key signature on the treble staff, in half spaces down from the top line.
const SharpSteps = [0, 3, -1, 2, 5, 1, 4];
const FlatSteps = [4, 1, 5, 2, 6, 3, 7];

/** Math.Round(x, digits): to the nearest, a midpoint to the even. */
function round(x: number, digits = 0): number {
    const scale = 10 ** digits;
    const scaled = x * scale;
    const floor = Math.floor(scaled);
    const rest = scaled - floor;
    const rounded = Math.abs(rest - 0.5) < 1e-9 ? (floor % 2 === 0 ? floor : floor + 1) : Math.round(scaled);
    return rounded / scale;
}

const N = (x: number): string => String(x);

const svg = (left: number, top: number, width: number, height: number, heightEm: number, content: string): string =>
    `<svg viewBox='${N(left)} ${N(top)} ${N(width)} ${N(height)}' height='${N(heightEm)}em' width='${N(round(heightEm * width / height, 3))}em' fill='currentColor' stroke='currentColor' aria-hidden='true'>${content}</svg>`;

/**
 * A mark in the flow of the text, on a line of its own: here the key, the tempo or the time signature changes (or is set, at the start).
 * Rendered as a musician writes them: the key signature on a piece of staff with the key's name, the time signature, the tempo as a note and its number.
 * The text and the chords know nothing of these marks: a sheet without them is read as before.
 *
 * Departure from the .NET side: its two inline styles (`white-space: nowrap` on the mark, `vertical-align: middle` on
 * the drawings) are left out, as the page's content policy allows no inline style; chords/chords.css carries both.
 */
export class NodeMark extends NodeBase {
    key: SheetKey | null = null;
    tempo: SheetTempo | null = null;
    timeSignature: SheetTimeSignature | null = null;
    /** When the change happens, in seconds from the start, when known. */
    time: number | null = null;

    /** `new NodeMark { Key = ..., Tempo = ..., TimeSignature = ..., Time = ... }` */
    constructor(init: { key?: SheetKey | null; tempo?: SheetTempo | null; timeSignature?: SheetTimeSignature | null; time?: number | null } = {}) {
        super();
        this.key = init.key ?? null;
        this.tempo = init.tempo ?? null;
        this.timeSignature = init.timeSignature ?? null;
        this.time = init.time ?? null;
    }

    override appendHtmlTo(builder: StringBuilder, representationSettings: RepresentationSettings, _traversalContext: TraversalContext): void {
        if (this.key === null && this.tempo === null && this.timeSignature === null)
            throw new InvalidOperationException('A mark carries a key, a tempo or a time signature.');

        builder.append('<span class=\'sheet-mark\'');
        if (this.time !== null) {
            builder.append(' ');
            builder.append(TimeAttribute.format(this.time));
        }

        builder.append('>');

        const parts: string[] = [];
        if (this.key !== null) parts.push(NodeMark.keyHtml(this.key.shown(representationSettings)));
        if (this.timeSignature !== null) parts.push(NodeMark.timeSignatureHtml(this.timeSignature));
        if (this.tempo !== null) parts.push(NodeMark.tempoHtml(this.tempo));
        builder.append(parts.join(' '));

        builder.append('</span>');
    }

    private static keyHtml(key: SheetKey): string {
        const name = htmlEncode(key.name)!;
        const start = `<span class='sheet-mark-key' data-key='${htmlAttributeEncode(key.name)}'>`;

        // A key of more than seven sharps or flats, as it is named, has no signature: its name alone.
        if (!key.hasSignature)
            return `${start}<span class='sheet-mark-key-name'>${name}</span></span>`;

        const signature = key.signature;
        const steps = signature >= 0 ? SharpSteps : FlatSteps;
        const count = Math.abs(signature);
        const width = Space + count * Space + Space;

        const content = new StringBuilder();
        for (let line = 0; line < 5; line++)
            content.append(`<line x1='0' x2='${width}' y1='${line * Space}' y2='${line * Space}' stroke-width='1' opacity='0.6'/>`);

        for (let i = 0; i < count; i++) {
            const x = Space + i * Space + Space / 2.0, y = steps[i]! * Space / 2.0;
            content.append(signature > 0 ? NodeMark.sharpSign(x, y) : NodeMark.flatSign(x, y));
        }

        // The staff is as tall as a line of text and a half; the signs stand out of it above and below.
        return `${start}${svg(0, -18, width, 62, 2.1, content.toString())} <span class='sheet-mark-key-name'>${name}</span></span>`;
    }

    private static sharpSign(x: number, y: number): string {
        const bar = (c: number) => `<polygon stroke='none' points='${N(x - 5)},${N(c + 1.5)} ${N(x + 5)},${N(c - 1.5)} ${N(x + 5)},${N(c + 1.5)} ${N(x - 5)},${N(c + 4.5)}'/>`;
        return `<line x1='${N(x - 2)}' x2='${N(x - 2)}' y1='${N(y - 9)}' y2='${N(y + 12)}' stroke-width='1.3'/>`
            + `<line x1='${N(x + 2)}' x2='${N(x + 2)}' y1='${N(y - 12)}' y2='${N(y + 9)}' stroke-width='1.3'/>`
            + bar(y - 6) + bar(y + 1);
    }

    private static flatSign(x: number, y: number): string {
        return `<line x1='${N(x - 3)}' x2='${N(x - 3)}' y1='${N(y - 16)}' y2='${N(y + 5)}' stroke-width='1.4'/>`
            + `<path fill='none' stroke-width='1.8' d='M ${N(x - 3)} ${N(y + 5)} C ${N(x + 1)} ${N(y + 2)} ${N(x + 6)} ${N(y - 2)} ${N(x + 3)} ${N(y - 4)} C ${N(x + 1)} ${N(y - 5.5)} ${N(x - 1)} ${N(y - 3)} ${N(x - 3)} ${N(y - 1)}'/>`;
    }

    private static timeSignatureHtml(timeSignature: SheetTimeSignature): string {
        const digits = Math.max(String(timeSignature.beats).length, String(timeSignature.unit).length);
        const width = 13 * digits + 2;
        const content = `<text stroke='none' x='${N(width / 2.0)}' y='18' text-anchor='middle' font-size='22' font-weight='bold' font-family='serif'>${timeSignature.beats}</text>`
            + `<text stroke='none' x='${N(width / 2.0)}' y='39' text-anchor='middle' font-size='22' font-weight='bold' font-family='serif'>${timeSignature.unit}</text>`;
        return `<span class='sheet-mark-time-signature' role='img' aria-label='${timeSignature.toString()}'>${svg(0, 0, width, 40, 1.4, content)}</span>`;
    }

    private static tempoHtml(tempo: SheetTempo): string {
        const flags = tempo.unit === 8 ? 1 : tempo.unit === 16 ? 2 : 0;
        const width = 13 + (flags > 0 ? 6 : 0) + (tempo.dotted ? 6 : 0);

        const content = new StringBuilder();
        const hollow = tempo.unit <= 2;
        content.append(`<ellipse cx='6' cy='32' rx='5' ry='3.6' transform='rotate(-20 6 32)' ${hollow ? 'fill=\'none\' stroke-width=\'1.6\'' : 'stroke=\'none\''}/>`);
        if (tempo.unit >= 2)
            content.append('<line x1=\'10.6\' x2=\'10.6\' y1=\'31\' y2=\'4\' stroke-width=\'1.4\'/>');

        for (let i = 0; i < flags; i++)
            content.append(`<path fill='none' stroke-width='1.6' d='M 10.6 ${4 + i * 6} C 12 ${10 + i * 6} 18 ${12 + i * 6} 16 ${20 + i * 6}'/>`);

        if (tempo.dotted)
            content.append(`<circle stroke='none' cx='${width - 3}' cy='32' r='1.7'/>`);

        const bpm = N(round(tempo.beatsPerMinute));
        return `<span class='sheet-mark-tempo' data-bpm='${N(tempo.beatsPerMinute)}'>${svg(0, 0, width, 40, 1.3, content.toString())} = ${bpm}</span>`;
    }

    override appendChordsTo(): void {
    }

    override appendLyricsTo(): void {
    }

    override appendHtmlAttributeTo(): void {
    }

    // The text form of the sheet has no marks, and leaves no trace of them: the space after a mark that stands between two words is dropped
    // (and a line of marks alone, see NodeHtml.asOneShelf).
    override appendOneShelfTo(builder: StringBuilder, _representationSettings: RepresentationSettings,
        _isBold: boolean, owesWhitespace: Ref<boolean>): void {
        if (builder.length > 0 && builder.charAt(builder.length - 1) === ' ')
            owesWhitespace.value = true;
    }

    override shallowClone(): NodeBase {
        return new NodeMark({ key: this.key, tempo: this.tempo, timeSignature: this.timeSignature, time: this.time });
    }

    static readonly json: JsonContract<NodeMark> = jsonClass<NodeMark>('HarmonyDB.Common.Representations.OneShelf.NodeMark', init => new NodeMark({
        key: init.key as SheetKey | null | undefined,
        tempo: init.tempo as SheetTempo | null | undefined,
        timeSignature: init.timeSignature as SheetTimeSignature | null | undefined,
        time: init.time as number | null | undefined,
    }), () => [
        jsonProperty('Key', () => SheetKey.json, { ignoreWhenWritingNull: true }),
        jsonProperty('Tempo', () => SheetTempo.json, { ignoreWhenWritingNull: true }),
        jsonProperty('TimeSignature', () => SheetTimeSignature.json, { ignoreWhenWritingNull: true }),
        jsonProperty('Time', jsonNullable(jsonDouble), { ignoreWhenWritingNull: true }),
    ]);
}
