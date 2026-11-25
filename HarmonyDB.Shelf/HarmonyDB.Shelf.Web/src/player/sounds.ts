// The metronome's click: a 30 ms sine burst (1500 Hz on a bar's first beat, 1000 Hz on the others), 1 ms attack,
// exponential decay. (The chords sound as every chord of the app does: chords/sound.ts.)

const clicks = new WeakMap<BaseAudioContext, { weak: AudioBuffer; strong: AudioBuffer }>();

function clickBuffer(context: BaseAudioContext, freq: number): AudioBuffer {
    const sr = context.sampleRate, length = Math.round(0.03 * sr), attack = sr / 1000, decay = length / 5;
    const buffer = context.createBuffer(1, length, sr), data = buffer.getChannelData(0);
    for (let i = 0; i < length; i++) data[i] = Math.min(1, i / attack) * Math.exp(-i / decay) * Math.sin(2 * Math.PI * freq * i / sr);
    return buffer;
}

/** One click at context time `when` into `destination`; `strong`: a bar's first beat. */
export function playClick(context: BaseAudioContext, destination: AudioNode, when: number, strong: boolean): AudioScheduledSourceNode[] {
    let buffers = clicks.get(context);
    if (!buffers) {
        buffers = { weak: clickBuffer(context, 1000), strong: clickBuffer(context, 1500) };
        clicks.set(context, buffers);
    }
    const source = context.createBufferSource();
    source.buffer = strong ? buffers.strong : buffers.weak;
    const gain = context.createGain();
    gain.gain.value = 0.5;
    source.connect(gain);
    gain.connect(destination);
    source.addEventListener('ended', () => gain.disconnect(), { once: true });
    source.start(when);
    return [source];
}

