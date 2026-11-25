// The tuner's pitch: a pure tone and a plucked-like tone with overtones are heard as their notes.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { detectPitch, nearestNote } from './pitch.ts';

const rate = 44100;

function tone(hz: number, overtones = false, length = 4096): Float32Array {
    const samples = new Float32Array(length);
    for (let i = 0; i < length; i++) {
        const t = i / rate;
        samples[i] = 0.5 * Math.sin(2 * Math.PI * hz * t) + (overtones ? 0.3 * Math.sin(4 * Math.PI * hz * t) + 0.2 * Math.sin(6 * Math.PI * hz * t) : 0);
    }
    return samples;
}

test('the strings of a guitar are heard as their notes', () => {
    for (const [hz, midi] of [[82.41, 40], [110, 45], [146.83, 50], [196, 55], [246.94, 59], [329.63, 64]] as const) {
        const heard = detectPitch(tone(hz, true), rate);
        assert.ok(heard !== null);
        const note = nearestNote(heard, 440);
        assert.equal(note.midi, midi);
        assert.ok(Math.abs(note.cents) <= 3, `${hz} Hz: ${note.cents} cents`);
    }
});

test('a slightly sharp A is heard as sharp', () => {
    const note = nearestNote(detectPitch(tone(442), rate)!, 440);
    assert.equal(note.midi, 69);
    assert.ok(note.cents >= 6 && note.cents <= 10);
});

test('silence has no pitch', () => {
    assert.equal(detectPitch(new Float32Array(4096), rate), null);
});
