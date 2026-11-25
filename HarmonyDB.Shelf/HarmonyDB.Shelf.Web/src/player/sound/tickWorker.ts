// A metronome for the scheduler. A worker's timer keeps its period when the page is hidden; the page's own timers and
// requestAnimationFrame do not.

/** The worker's own scope (the app's types are the page's). */
const scope = self as unknown as { onmessage: ((event: MessageEvent<number>) => void) | null; postMessage(message: number): void };

let timer: ReturnType<typeof setInterval> | null = null;

scope.onmessage = event => {
    const period = event.data;
    if (timer !== null) {
        clearInterval(timer);
        timer = null;
    }
    if (period > 0) timer = setInterval(() => scope.postMessage(0), period);
};
