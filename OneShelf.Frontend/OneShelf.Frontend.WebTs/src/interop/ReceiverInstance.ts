// The original: Interop/ReceiverInstance.cs.

export class ReceiverInstance {
    private readonly _onDispose: (instance: ReceiverInstance) => void;
    private _actionChordClick: ((chordData: string, chordIndex: number) => Promise<void>) | null = null;

    constructor(onDispose: (instance: ReceiverInstance) => void) {
        this._onDispose = onDispose;
    }

    setChordClick(action: (chordData: string, chordIndex: number) => Promise<void>): void {
        this._actionChordClick = action;
    }

    async callChordClick(chordData: string, chordIndex: number): Promise<void> {
        if (this._actionChordClick !== null) await this._actionChordClick(chordData, chordIndex);
    }

    dispose(): void {
        this._onDispose(this);
    }
}
