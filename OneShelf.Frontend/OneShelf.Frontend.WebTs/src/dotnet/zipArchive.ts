// System.IO.Compression.ZipArchive in ZipArchiveMode.Create, as Pages/Print.razor uses it: entries made one after
// another with CreateEntry(name) (CompressionLevel.Optimal: deflate), written whole, the archive closed into bytes.
//
// The archive has the same entries, names, flags and header fields as the original's (version made by 0x0314, external
// attributes 0x81A40000: a regular file rw-r--r--, UTF-8 flag on non-ASCII names, as .NET in the browser writes them);
// its bytes are not .NET's: the deflate stream is the browser's (CompressionStream 'deflate-raw'), not zlib-ng's.

const crcTable = (() => {
    const table = new Uint32Array(256);
    for (let n = 0; n < 256; n++) {
        let c = n;
        for (let k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1;
        table[n] = c >>> 0;
    }

    return table;
})();

function crc32(data: Uint8Array): number {
    let crc = 0xFFFFFFFF;
    for (let i = 0; i < data.length; i++) crc = crcTable[(crc ^ data[i]!) & 0xFF]! ^ (crc >>> 8);
    return (crc ^ 0xFFFFFFFF) >>> 0;
}

async function deflateRaw(data: Uint8Array): Promise<Uint8Array> {
    const stream = new Blob([data]).stream().pipeThrough(new CompressionStream('deflate-raw'));
    return new Uint8Array(await new Response(stream).arrayBuffer());
}

/** DateTimeOffset.Now as an MS-DOS date and time (local time, two-second resolution). */
function dosDateTime(date: Date): { time: number; date: number } {
    return {
        time: (date.getHours() << 11) | (date.getMinutes() << 5) | Math.floor(date.getSeconds() / 2),
        date: ((date.getFullYear() - 1980) << 9) | ((date.getMonth() + 1) << 5) | date.getDate(),
    };
}

interface Entry {
    name: Uint8Array;
    utf8: boolean;
    time: { time: number; date: number };
    crc: number;
    compressed: Uint8Array;
    size: number;
    offset: number;
}

export class ZipArchive {
    private readonly entries: Entry[] = [];

    /** CreateEntry(name) and writing the whole content to its stream. */
    async addEntry(name: string, content: Uint8Array): Promise<void> {
        const time = dosDateTime(new Date());
        const encodedName = new TextEncoder().encode(name);
        // eslint-disable-next-line no-control-regex
        const utf8 = /[^\x00-\x7F]/.test(name);
        this.entries.push({ name: encodedName, utf8, time, crc: crc32(content), compressed: await deflateRaw(content), size: content.length, offset: 0 });
    }

    /** Disposing the archive: the local headers with the data, then the central directory. */
    toBytes(): Uint8Array {
        const parts: Uint8Array[] = [];
        let offset = 0;
        for (const entry of this.entries) {
            entry.offset = offset;
            const header = new DataView(new ArrayBuffer(30));
            header.setUint32(0, 0x04034B50, true);
            header.setUint16(4, 20, true);
            header.setUint16(6, entry.utf8 ? 0x0800 : 0, true);
            header.setUint16(8, 8, true);
            header.setUint16(10, entry.time.time, true);
            header.setUint16(12, entry.time.date, true);
            header.setUint32(14, entry.crc, true);
            header.setUint32(18, entry.compressed.length, true);
            header.setUint32(22, entry.size, true);
            header.setUint16(26, entry.name.length, true);
            header.setUint16(28, 0, true);
            parts.push(new Uint8Array(header.buffer), entry.name, entry.compressed);
            offset += 30 + entry.name.length + entry.compressed.length;
        }

        const centralDirectoryOffset = offset;
        for (const entry of this.entries) {
            const header = new DataView(new ArrayBuffer(46));
            header.setUint32(0, 0x02014B50, true);
            header.setUint16(4, 0x0314, true);
            header.setUint16(6, 20, true);
            header.setUint16(8, entry.utf8 ? 0x0800 : 0, true);
            header.setUint16(10, 8, true);
            header.setUint16(12, entry.time.time, true);
            header.setUint16(14, entry.time.date, true);
            header.setUint32(16, entry.crc, true);
            header.setUint32(20, entry.compressed.length, true);
            header.setUint32(24, entry.size, true);
            header.setUint16(28, entry.name.length, true);
            header.setUint16(30, 0, true);
            header.setUint16(32, 0, true);
            header.setUint16(34, 0, true);
            header.setUint16(36, 0, true);
            header.setUint32(38, 0x81A40000, true);
            header.setUint32(42, entry.offset, true);
            parts.push(new Uint8Array(header.buffer), entry.name);
            offset += 46 + entry.name.length;
        }

        const end = new DataView(new ArrayBuffer(22));
        end.setUint32(0, 0x06054B50, true);
        end.setUint16(8, this.entries.length, true);
        end.setUint16(10, this.entries.length, true);
        end.setUint32(12, offset - centralDirectoryOffset, true);
        end.setUint32(16, centralDirectoryOffset, true);
        parts.push(new Uint8Array(end.buffer));

        const result = new Uint8Array(offset + 22);
        let position = 0;
        for (const part of parts) {
            result.set(part, position);
            position += part.length;
        }

        return result;
    }
}
