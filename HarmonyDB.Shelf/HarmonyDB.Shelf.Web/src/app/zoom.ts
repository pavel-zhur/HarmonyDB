// The two sizes, by two fingers (and Ctrl + wheel on a computer): on the chords (an element marked data-chords) the
// chords' size, anywhere else the whole interface's. The size follows the fingers at once (a CSS variable, nothing
// re-rendered) and is kept in the settings when the fingers leave. The layout re-flows to the width, so no
// horizontal scroll appears.

import { i18n } from '../i18n';
import { readSettings, updateSettings } from '../data/settings';
import { toast } from './toasts';
import { clampScale, setChordsScale, setInterfaceScale } from './appearance';

function current(kind: 'interface' | 'chords'): number {
    const settings = readSettings();
    return kind === 'interface' ? settings.interfaceScale : settings.chordsScale;
}

function set(kind: 'interface' | 'chords', value: number): void {
    if (kind === 'interface') setInterfaceScale(value);
    else setChordsScale(value);
}

function keep(kind: 'interface' | 'chords', value: number): void {
    updateSettings(kind === 'interface' ? { interfaceScale: value } : { chordsScale: value });
    if (kind === 'interface') toast(i18n.t('zoom.interface', { value: Math.round(value * 100) }));
}

const distance = (touches: TouchList) => Math.hypot(touches[0]!.clientX - touches[1]!.clientX, touches[0]!.clientY - touches[1]!.clientY);
const kindOf = (target: EventTarget | null) => (target instanceof Element && target.closest('[data-chords]') ? 'chords' : 'interface');

export function installZoom(): void {
    let pinch: { kind: 'interface' | 'chords'; distance: number; from: number; value: number } | null = null;

    document.addEventListener('touchstart', event => {
        if (event.touches.length !== 2) return;
        const kind = kindOf(event.target);
        const from = current(kind);
        pinch = { kind, distance: distance(event.touches), from, value: from };
    }, { passive: true });

    document.addEventListener('touchmove', event => {
        if (!pinch || event.touches.length !== 2) return;
        event.preventDefault();
        pinch.value = clampScale(pinch.kind, pinch.from * distance(event.touches) / pinch.distance);
        set(pinch.kind, pinch.value);
    }, { passive: false });

    const end = () => {
        if (!pinch) return;
        const { kind, value, from } = pinch;
        pinch = null;
        if (value !== from) keep(kind, value);
    };
    document.addEventListener('touchend', end);
    document.addEventListener('touchcancel', end);

    let wheelTimer: ReturnType<typeof setTimeout> | undefined;
    let wheel: { kind: 'interface' | 'chords'; value: number } | null = null;
    document.addEventListener('wheel', event => {
        if (!event.ctrlKey) return;
        event.preventDefault();
        const kind = kindOf(event.target);
        const value = clampScale(kind, (wheel?.kind === kind ? wheel.value : current(kind)) * (event.deltaY < 0 ? 1.06 : 0.94));
        wheel = { kind, value };
        set(kind, value);
        clearTimeout(wheelTimer);
        wheelTimer = setTimeout(() => {
            if (wheel) keep(wheel.kind, wheel.value);
            wheel = null;
        }, 400);
    }, { passive: false });
}
