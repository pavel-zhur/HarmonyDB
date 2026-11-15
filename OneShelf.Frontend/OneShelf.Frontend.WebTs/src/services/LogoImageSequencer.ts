// The original: Services/LogoImageSequencer.cs — every 15 seconds the next logo picture class, starting at a random one.

import { DelegateEvent } from '../dotnet/events';
import { Timer } from '../dotnet/tasks';

export class LogoImageSequencer {
    private readonly _sequence: { x: number; y: number }[];
    private _currentIndex: number;
    private readonly _timer: Timer;

    constructor() {
        const oneSequence = [
            1, 2, 3, 6, 5, 4, 7, 8, 9,
        ];

        this._sequence = [
            { x: 1, reverse: true },
            { x: 2, reverse: false },
            { x: 3, reverse: true },
            { x: 4, reverse: false },
            { x: 5, reverse: true },
        ].flatMap(x => (x.reverse ? [...oneSequence].reverse() : oneSequence).map(y => ({ x: x.x, y })));

        // Random.Shared.Next(0, _sequence.Count)
        this._currentIndex = Math.floor(Math.random() * this._sequence.length);

        const period = 0.25 * 60 * 1000;
        this._timer = new Timer(this.timerUpdate, period, period);

        this.current = LogoImageSequencer.getClass(this._sequence[this._currentIndex]!);
    }

    current: string;

    readonly change = new DelegateEvent();

    private readonly timerUpdate = (): void => {
        this._currentIndex = (this._currentIndex + 1) % this._sequence.length;

        this.current = LogoImageSequencer.getClass(this._sequence[this._currentIndex]!);

        this.onChange();
    };

    private static getClass(item: { x: number; y: number }): string {
        const { x, y } = item;
        return `l${x} l${x}-${y}`;
    }

    protected onChange(): void {
        this.change.invoke();
    }

    dispose(): void {
        this._timer.dispose();
    }
}