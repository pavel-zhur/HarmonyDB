// BinaryReader / BinaryWriter over a MemoryStream: little-endian, strings as a 7-bit encoded UTF-8 byte length and UTF-8.

import { EndOfStreamException, FormatException, IOException } from './exceptions';

const utf8Encoder = new TextEncoder();
// ignoreBOM: a leading U+FEFF stays in the string, as UTF8Encoding.GetString keeps it.
const utf8Decoder = new TextDecoder('utf-8', { ignoreBOM: true });

export class BinaryWriter {
    private buffer = new Uint8Array(64);
    private view = new DataView(this.buffer.buffer);
    private length = 0;

    private ensure(extra: number): void {
        if (this.length + extra <= this.buffer.length) return;
        let capacity = this.buffer.length * 2;
        while (capacity < this.length + extra) capacity *= 2;
        const buffer = new Uint8Array(capacity);
        buffer.set(this.buffer.subarray(0, this.length));
        this.buffer = buffer;
        this.view = new DataView(buffer.buffer);
    }

    writeByte(value: number): void {
        this.ensure(1);
        this.buffer[this.length++] = value & 0xff;
    }

    writeInt32(value: number): void {
        this.ensure(4);
        this.view.setInt32(this.length, value, true);
        this.length += 4;
    }

    writeSingle(value: number): void {
        this.ensure(4);
        this.view.setFloat32(this.length, value, true);
        this.length += 4;
    }

    write7BitEncodedInt(value: number): void {
        let uValue = value >>> 0;
        while (uValue > 0x7f) {
            this.writeByte((uValue | ~0x7f) & 0xff);
            uValue >>>= 7;
        }

        this.writeByte(uValue);
    }

    writeString(value: string): void {
        const bytes = utf8Encoder.encode(value);
        this.write7BitEncodedInt(bytes.length);
        this.ensure(bytes.length);
        this.buffer.set(bytes, this.length);
        this.length += bytes.length;
    }

    /** MemoryStream.ToArray(). */
    toArray(): Uint8Array {
        return this.buffer.slice(0, this.length);
    }
}

export class BinaryReader {
    private readonly view: DataView;
    private position = 0;

    constructor(private readonly data: Uint8Array) {
        this.view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    }

    private take(count: number): number {
        if (this.position + count > this.data.length) throw new EndOfStreamException();
        const position = this.position;
        this.position += count;
        return position;
    }

    readByte(): number {
        return this.data[this.take(1)]!;
    }

    readInt32(): number {
        return this.view.getInt32(this.take(4), true);
    }

    readSingle(): number {
        return this.view.getFloat32(this.take(4), true);
    }

    read7BitEncodedInt(): number {
        let result = 0;
        let byteReadJustNow: number;

        const maxBytesWithoutOverflow = 4;
        for (let shift = 0; shift < maxBytesWithoutOverflow * 7; shift += 7) {
            byteReadJustNow = this.readByte();
            result |= (byteReadJustNow & 0x7f) << shift;

            if (byteReadJustNow <= 0x7f) {
                return result | 0;
            }
        }

        byteReadJustNow = this.readByte();
        if (byteReadJustNow > 0b1111) {
            throw new FormatException('Too many bytes in what should have been a 7-bit encoded integer.');
        }

        result |= byteReadJustNow << (maxBytesWithoutOverflow * 7);
        return result | 0;
    }

    readString(): string {
        const length = this.read7BitEncodedInt();
        if (length < 0) throw new IOException(`BinaryReader encountered an invalid string length of ${length} characters.`);
        if (length === 0) return '';
        const start = this.take(length);
        return utf8Decoder.decode(this.data.subarray(start, start + length));
    }
}
