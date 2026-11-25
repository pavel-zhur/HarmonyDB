// The page side of the tempo's stretcher: loads its worklet module into a context and makes the node.
//
// To the player it is a node — stereo in, stereo out — with a fixed delay and three parameters:
//   ratio   output frequency / input frequency, for the input from the moment it is set
//   epoch   any change empties it at that block
//   run     0: it stands still — takes nothing in, gives silence, keeps what it holds

import processorUrl from './stretchProcessor.ts?worker&url';
import { LATENCY_SECONDS, PROCESSOR_NAME } from './stretchConstants';

export interface Shifter {
    node: AudioWorkletNode;
    latencyFrames: number;
    ratio: AudioParam;
    epoch: AudioParam;
    run: AudioParam;
}

/** Loads the worklet and makes the stretcher. `onProblem(error)`: it ran out of sound in the middle, or stopped. */
export async function createShifter(context: AudioContext, onProblem: (error: Error) => void): Promise<Shifter> {
    await context.audioWorklet.addModule(processorUrl);
    const node = new AudioWorkletNode(context, PROCESSOR_NAME, {
        numberOfInputs: 1,
        numberOfOutputs: 1,
        outputChannelCount: [2],
        channelCount: 2,
        channelCountMode: 'explicit',
        channelInterpretation: 'speakers',
    });
    node.addEventListener('processorerror', () => onProblem(new Error('the tempo stretcher stopped with an error')));
    node.port.onmessage = event => {
        const data = event.data as { type: string; count: number };
        if (data.type === 'underrun') onProblem(new Error(`the tempo stretcher ran out of sound (${data.count} times so far)`));
    };
    return {
        node,
        latencyFrames: Math.round(LATENCY_SECONDS * context.sampleRate),
        ratio: node.parameters.get('ratio')!,
        epoch: node.parameters.get('epoch')!,
        run: node.parameters.get('run')!,
    };
}

export function disposeShifter(shifter: Shifter): void {
    shifter.node.port.postMessage('dispose');
    shifter.node.port.onmessage = null;
    shifter.node.disconnect();
}

/** The stretcher's ratio for a tempo and a transposition: undo the sources' speed, then transpose. */
export function shifterRatio(tempo: number, semitones: number): number {
    return Math.pow(2, semitones / 12) / tempo;
}
