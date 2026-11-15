import { htmlAttributeEncode, htmlEncode } from '../common/dotnet/html';
import { jsonClass, jsonProperty, jsonString, type JsonContract } from '../common/dotnet/json/JsonSerializer';
import type { StringBuilder } from '../common/dotnet/StringBuilder';
import { substring } from '../common/dotnet/text';
import type { RepresentationSettings } from './RepresentationSettings';
import type { TraversalContext } from './TraversalContext';
import { NodeBase, type Ref } from './NodeBase';
import './internal'; // fills nodeTypes, which the base class uses

// .NET: (?<up>…)|\((?<up>…)\)|(?<up>[5oO]$)|(?<down>…) — one name used three times; here up1, up2, up3.
// .NET's \d is any Unicode decimal digit, and $ also matches before a final \n.
const beautifyPattern = /(?<up1>(b|#|-|\+)(5|6|7|9|11|13)|(5|6|7|9|11|13)(b|#|-|\+)(?!\p{Nd})|(\([^\(\)]+\)){2,})|\((?<up2>[^\(\)]+)\)|(?<up3>[5oO](?=\n?$))|(?<down>[12346789]+?)/gu;

export class NodeText extends NodeBase {
    readonly text: string;

    constructor(text: string) {
        super();
        this.text = text.replace(/\r/g, '');
    }

    override appendHtmlTo(builder: StringBuilder, _representationSettings: RepresentationSettings,
        traversalContext: TraversalContext): void {
        builder.append(traversalContext.isInsideChord ? this.beautify(this.text) : htmlEncode(this.text));
    }

    private beautify(text: string): string {
        return text.replace(beautifyPattern, (value: string, ...args: unknown[]) => {
            const groups = args[args.length - 1] as Record<string, string | undefined>;
            const up = groups.up1 ?? groups.up2 ?? groups.up3;
            if (up !== undefined) {
                return `<sup>${htmlEncode(up)}</sup>`;
            }

            if (groups.down !== undefined) {
                return `<sub>${htmlEncode(groups.down)}</sub>`;
            }

            return htmlEncode(value)!;
        });
    }

    override appendChordsTo(): void {
    }

    override appendLyricsTo(builder: StringBuilder): void {
        builder.append(this.text);
    }

    override appendHtmlAttributeTo(builder: StringBuilder): void {
        builder.append(htmlAttributeEncode(this.text));
    }

    override appendOneShelfTo(builder: StringBuilder, _representationSettings: RepresentationSettings,
        _isBold: boolean, owesWhitespace: Ref<boolean>): void {
        if (owesWhitespace.value && this.text.startsWith(' ') && this.text.length > 1) {
            builder.append(substring(this.text, 1));
            owesWhitespace.value = false;
        } else {
            builder.append(this.text);
        }
    }

    override shallowClone(): NodeBase {
        return new NodeText(this.text);
    }

    static readonly json: JsonContract<NodeText> = jsonClass<NodeText>('HarmonyDB.Common.Representations.OneShelf.NodeText', init => new NodeText(init.text as string), () => [
        jsonProperty('Text', jsonString),
    ]);
}
