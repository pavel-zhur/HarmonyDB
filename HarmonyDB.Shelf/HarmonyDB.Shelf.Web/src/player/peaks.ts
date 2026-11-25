// The waveform's numbers: the lowest and the highest sample of any stretch of a decoded recording, fast enough to draw
// from at any zoom.
//
//   const peaks = new Peaks(audioBuffer);
//   peaks.draw(ctx, { x, y, width, height, from, to, color })   the waveform of from…to (seconds), one column a pixel
//
// A pyramid of min/max pairs is built once (256 samples a pair, then four times coarser each level). A column narrower
// than the finest level is read from the samples themselves, so the picture stays exact however far it is zoomed in.

const BASE = 256, STEP = 4;

interface Level { size: number; min: Float32Array; max: Float32Array }

export class Peaks {
    private readonly rate: number;
    private readonly length: number;
    private readonly channels: Float32Array[] = [];
    private readonly levels: Level[];
    /** The loudest sample; the drawing is scaled so that it fills the height. */
    private readonly peak: number;

    constructor(buffer: AudioBuffer) {
        this.rate = buffer.sampleRate;
        this.length = buffer.length;
        for (let c = 0; c < buffer.numberOfChannels; c++) this.channels.push(buffer.getChannelData(c));

        const n = Math.ceil(this.length / BASE);
        const min = new Float32Array(n), max = new Float32Array(n);
        for (let i = 0; i < n; i++) {
            const a = i * BASE, b = Math.min(this.length, a + BASE);
            let lo = 1, hi = -1;
            for (const data of this.channels) {
                for (let j = a; j < b; j++) {
                    const v = data[j]!;
                    if (v < lo) lo = v;
                    if (v > hi) hi = v;
                }
            }
            min[i] = lo;
            max[i] = hi;
        }
        this.levels = [{ size: BASE, min, max }];
        for (let level = this.levels[0]!; level.min.length > 64;) {
            const m = Math.ceil(level.min.length / STEP), lmin = new Float32Array(m), lmax = new Float32Array(m);
            for (let i = 0; i < m; i++) {
                let lo = 1, hi = -1;
                for (let j = i * STEP, e = Math.min(level.min.length, j + STEP); j < e; j++) {
                    if (level.min[j]! < lo) lo = level.min[j]!;
                    if (level.max[j]! > hi) hi = level.max[j]!;
                }
                lmin[i] = lo;
                lmax[i] = hi;
            }
            level = { size: level.size * STEP, min: lmin, max: lmax };
            this.levels.push(level);
        }
        let peak = 0;
        const top = this.levels[this.levels.length - 1]!;
        for (let i = 0; i < top.min.length; i++) peak = Math.max(peak, -top.min[i]!, top.max[i]!);
        this.peak = peak;
    }

    /** [min, max] of the samples from frame a up to frame b. */
    private frames(a: number, b: number): [number, number] {
        a = Math.max(0, Math.floor(a));
        b = Math.min(this.length, Math.ceil(b));
        if (b <= a) return [0, 0];
        let lo = 1, hi = -1;
        if (b - a < BASE * 2) {
            for (const data of this.channels) {
                for (let j = a; j < b; j++) {
                    const v = data[j]!;
                    if (v < lo) lo = v;
                    if (v > hi) hi = v;
                }
            }
            return [lo, hi];
        }
        let level = this.levels[0]!;
        for (const candidate of this.levels) {
            if (candidate.size * 2 <= b - a) level = candidate;
            else break;
        }
        const i0 = Math.floor(a / level.size), i1 = Math.min(level.min.length, Math.ceil(b / level.size));
        for (let i = i0; i < i1; i++) {
            if (level.min[i]! < lo) lo = level.min[i]!;
            if (level.max[i]! > hi) hi = level.max[i]!;
        }
        return [lo, hi];
    }

    draw(ctx: CanvasRenderingContext2D, { x, y, width, height, from, to, color }: { x: number; y: number; width: number; height: number; from: number; to: number; color: string }): void {
        if (!(this.peak > 0) || width <= 0) return;
        const mid = y + height / 2, scale = (height / 2) / this.peak;
        const perColumn = (to - from) * this.rate / width;
        ctx.fillStyle = color;
        for (let i = 0; i < width; i++) {
            const a = from * this.rate + i * perColumn;
            if (a >= this.length || a + perColumn <= 0) continue;
            const [lo, hi] = this.frames(a, a + perColumn);
            const top = mid - hi * scale, bottom = mid - lo * scale;
            ctx.fillRect(x + i, top, 1, Math.max(1, bottom - top));
        }
    }
}
