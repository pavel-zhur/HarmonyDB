// The audio worklet side of the tempo's stretcher: loaded with audioWorklet.addModule() (shifter.ts), it runs the pipe
// on the audio thread.

import { StretchPipe } from './stretchPipe';
import { MAX_RATIO, MIN_RATIO, PROCESSOR_NAME } from './stretchConstants';

// the audio worklet's own scope (not in the DOM's types)
declare const sampleRate: number;
declare class AudioWorkletProcessor {
    readonly port: MessagePort;
}
declare function registerProcessor(name: string, processor: new () => AudioWorkletProcessor): void;

type Params = Record<'ratio' | 'epoch' | 'run', Float32Array>;

class StretchProcessor extends AudioWorkletProcessor {
    static get parameterDescriptors() {
        return [
            // output frequency / input frequency, for the input from the moment it is set
            { name: 'ratio', defaultValue: 1, minValue: MIN_RATIO, maxValue: MAX_RATIO, automationRate: 'k-rate' },
            // any change drops what is inside at that block
            { name: 'epoch', defaultValue: 0, automationRate: 'k-rate' },
            // 0: it stands still — takes nothing in, gives silence, keeps what it holds
            { name: 'run', defaultValue: 1, minValue: 0, maxValue: 1, automationRate: 'k-rate' },
        ];
    }

    private readonly pipe = new StretchPipe(sampleRate);
    private epoch = 0;
    private ratio = 1;
    private reported = 0;
    private alive = true;

    constructor() {
        super();
        this.port.onmessage = event => {
            if (event.data === 'dispose') this.alive = false;
        };
    }

    process(inputs: Float32Array[][], outputs: Float32Array[][], parameters: Params): boolean {
        const pipe = this.pipe, output = outputs[0]!;
        const outL = output[0]!, outR = output[1]!;
        if (parameters.run[0]! < 0.5) {
            outL.fill(0);
            outR.fill(0);
            return this.alive;
        }
        const epoch = parameters.epoch[0]!;
        if (epoch !== this.epoch) {
            this.epoch = epoch;
            pipe.flush();
        }
        const ratio = parameters.ratio[0]!;
        if (ratio !== this.ratio) {
            this.ratio = ratio;
            pipe.setRatio(ratio);
        }

        const input = inputs[0]!;
        const inL = input.length > 0 ? input[0]! : null;
        const inR = input.length > 1 ? input[1]! : inL;
        pipe.process(inL, inR, outL, outR, outL.length);

        if (pipe.underruns !== this.reported) {
            this.reported = pipe.underruns;
            this.port.postMessage({ type: 'underrun', count: pipe.underruns });
        }
        return this.alive;
    }
}

registerProcessor(PROCESSOR_NAME, StretchProcessor);
