// A JSON reader that keeps what System.Text.Json sees and JSON.parse loses: the order of object members
// (JSON.parse moves integer-like keys first) and the exact text of numbers.
// Like Utf8JsonReader with the default options: strict RFC 8259, no comments, no trailing commas, max depth 64.

import { InvalidOperationException, JsonException } from '../exceptions';

export enum JsonValueKind {
    Undefined = 0,
    Object = 1,
    Array = 2,
    String = 3,
    Number = 4,
    True = 5,
    False = 6,
    Null = 7,
}

export type JsonNode =
    | { readonly kind: JsonValueKind.Object; readonly entries: readonly (readonly [string, JsonNode])[] }
    | { readonly kind: JsonValueKind.Array; readonly items: readonly JsonNode[] }
    | { readonly kind: JsonValueKind.String; readonly value: string }
    | { readonly kind: JsonValueKind.Number; readonly raw: string }
    | { readonly kind: JsonValueKind.True }
    | { readonly kind: JsonValueKind.False }
    | { readonly kind: JsonValueKind.Null };

const maxDepth = 64;

class Parser {
    private position = 0;
    private depth = 0;

    constructor(private readonly text: string) {
    }

    parseDocument(): JsonNode {
        this.skipWhiteSpace();
        if (this.position >= this.text.length) this.fail('The input does not contain any JSON tokens.');
        const node = this.parseValue();
        this.skipWhiteSpace();
        if (this.position < this.text.length) this.fail('\'' + this.text[this.position] + '\' is invalid after a single JSON value. Expected end of data.');
        return node;
    }

    private fail(message: string): never {
        throw new JsonException(`${message} BytePositionInLine: ${this.position}.`);
    }

    private skipWhiteSpace() {
        while (this.position < this.text.length) {
            const c = this.text.charCodeAt(this.position);
            if (c === 0x20 || c === 0x09 || c === 0x0a || c === 0x0d) this.position++;
            else break;
        }
    }

    private parseValue(): JsonNode {
        const c = this.text[this.position];
        switch (c) {
            case '{': return this.parseObject();
            case '[': return this.parseArray();
            case '"': return { kind: JsonValueKind.String, value: this.parseString() };
            case 't': this.expectLiteral('true'); return { kind: JsonValueKind.True };
            case 'f': this.expectLiteral('false'); return { kind: JsonValueKind.False };
            case 'n': this.expectLiteral('null'); return { kind: JsonValueKind.Null };
            default:
                if (c === '-' || (c !== undefined && c >= '0' && c <= '9')) return this.parseNumber();
                return this.fail(c === undefined ? 'Expected a value, but reached the end of data.' : `'${c}' is an invalid start of a value.`);
        }
    }

    private expectLiteral(literal: string) {
        if (this.text.startsWith(literal, this.position)) {
            this.position += literal.length;
        } else {
            this.fail(`'${literal}' is an invalid JSON literal.`);
        }
    }

    private enter() {
        if (++this.depth > maxDepth) this.fail(`The maximum configured depth of ${maxDepth} has been exceeded.`);
    }

    private parseObject(): JsonNode {
        this.enter();
        this.position++;
        const entries: [string, JsonNode][] = [];
        this.skipWhiteSpace();
        if (this.text[this.position] === '}') {
            this.position++;
            this.depth--;
            return { kind: JsonValueKind.Object, entries };
        }
        while (true) {
            this.skipWhiteSpace();
            if (this.text[this.position] !== '"') this.fail('Expected a property name.');
            const name = this.parseString();
            this.skipWhiteSpace();
            if (this.text[this.position] !== ':') this.fail('Expected a \':\' after a property name.');
            this.position++;
            this.skipWhiteSpace();
            entries.push([name, this.parseValue()]);
            this.skipWhiteSpace();
            const c = this.text[this.position];
            if (c === ',') {
                this.position++;
                continue;
            }
            if (c === '}') {
                this.position++;
                this.depth--;
                return { kind: JsonValueKind.Object, entries };
            }
            this.fail('Expected a \',\' or \'}\' after a property value.');
        }
    }

    private parseArray(): JsonNode {
        this.enter();
        this.position++;
        const items: JsonNode[] = [];
        this.skipWhiteSpace();
        if (this.text[this.position] === ']') {
            this.position++;
            this.depth--;
            return { kind: JsonValueKind.Array, items };
        }
        while (true) {
            this.skipWhiteSpace();
            items.push(this.parseValue());
            this.skipWhiteSpace();
            const c = this.text[this.position];
            if (c === ',') {
                this.position++;
                continue;
            }
            if (c === ']') {
                this.position++;
                this.depth--;
                return { kind: JsonValueKind.Array, items };
            }
            this.fail('Expected a \',\' or \']\' after an array value.');
        }
    }

    private parseString(): string {
        this.position++;
        let result = '';
        let start = this.position;
        while (true) {
            if (this.position >= this.text.length) this.fail('Expected end of string, but instead reached end of data.');
            const c = this.text.charCodeAt(this.position);
            if (c === 0x22) {
                result += this.text.substring(start, this.position);
                this.position++;
                return result;
            }
            if (c < 0x20) this.fail('Invalid control character within a string.');
            if (c === 0x5c) {
                result += this.text.substring(start, this.position);
                this.position++;
                const e = this.text[this.position];
                switch (e) {
                    case '"': result += '"'; break;
                    case '\\': result += '\\'; break;
                    case '/': result += '/'; break;
                    case 'b': result += '\b'; break;
                    case 'f': result += '\f'; break;
                    case 'n': result += '\n'; break;
                    case 'r': result += '\r'; break;
                    case 't': result += '\t'; break;
                    case 'u': {
                        const hex = this.text.substring(this.position + 1, this.position + 5);
                        if (!/^[0-9a-fA-F]{4}$/.test(hex)) this.fail('Invalid unicode escape.');
                        const code = parseInt(hex, 16);
                        this.position += 4;
                        // A surrogate must be escaped as a whole pair: System.Text.Json does not read a lone one.
                        if (code >= 0xdc00 && code <= 0xdfff) this.fail('Cannot read invalid UTF-16 JSON text as string.');
                        if (code >= 0xd800 && code <= 0xdbff) {
                            const low = /^\\u([dD][c-fC-F][0-9a-fA-F]{2})/.exec(this.text.substring(this.position + 1, this.position + 7));
                            if (low === null) this.fail('Cannot read invalid UTF-16 JSON text as string.');
                            result += String.fromCharCode(code, parseInt(low[1]!, 16));
                            this.position += 6;
                            break;
                        }
                        result += String.fromCharCode(code);
                        break;
                    }
                    default: this.fail('Invalid escape character within a string.');
                }
                this.position++;
                start = this.position;
                continue;
            }
            this.position++;
        }
    }

    private parseNumber(): JsonNode {
        const match = /-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?/y;
        match.lastIndex = this.position;
        const m = match.exec(this.text);
        if (!m || m[0] === '-') this.fail('Invalid number.');
        this.position += m[0].length;
        const next = this.text[this.position];
        if (next !== undefined && /[0-9.eE+-]/.test(next)) this.fail('Invalid number.');
        return { kind: JsonValueKind.Number, raw: m[0] };
    }
}

/** Parses a JSON text as System.Text.Json reads it. */
export function parseJson(text: string): JsonNode {
    return new Parser(text).parseDocument();
}

/** System.Text.Json.JsonElement: what a property of type `object` holds after deserialization. */
export class JsonElement {
    constructor(readonly node: JsonNode) {
    }

    get valueKind(): JsonValueKind {
        return this.node.kind;
    }

    /** JsonElement.GetString(): the string, null for a JSON null, otherwise InvalidOperationException. */
    getString(): string | null {
        if (this.node.kind === JsonValueKind.String) return this.node.value;
        if (this.node.kind === JsonValueKind.Null) return null;
        throw new InvalidOperationException(`The requested operation requires an element of type 'String', but the target element has type '${JsonValueKind[this.node.kind]}'.`);
    }
}
