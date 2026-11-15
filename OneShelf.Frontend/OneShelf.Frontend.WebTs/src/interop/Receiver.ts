// The original: Interop/Receiver.cs.
//
// Reference is what the original passes to JavaScript as DotNetObjectReference.Create(this): the Telegram widget
// (Login.razor.js) calls OnTelegramAuth with the user object, ChordsView.razor.js calls OnChordClick.
// OnTelegramAuth's `object user` arrives as a JsonElement, whose ToString() is the JSON text JavaScript sent: here
// that text (JSON.stringify of the user), which IdentityProvider.Set stores.

import { AsyncLock } from '../packages/Nito.AsyncEx/AsyncLock';
import type { IdentityProvider } from '../services/IdentityProvider';
import { createDotNetObjectReference, type DotNetObjectReference } from './jsRuntime';
import { ReceiverInstance } from './ReceiverInstance';

export class Receiver {
    private readonly _instances: ReceiverInstance[] = [];
    private readonly _instancesLock = new AsyncLock();
    private readonly _identityProvider: IdentityProvider;

    readonly reference: DotNetObjectReference;

    constructor(identityProvider: IdentityProvider) {
        this._identityProvider = identityProvider;
        this.reference = createDotNetObjectReference({
            OnTelegramAuth: (user: unknown) => this.onTelegramAuth(JSON.stringify(user)),
            OnChordClick: (chordData: string, chordIndex: number) => this.onChordClick(chordData, chordIndex),
        });
    }

    /** [JSInvokable] — `user` is the JSON text of the object JavaScript passed. */
    async onTelegramAuth(user: string): Promise<void> {
        await this._identityProvider.set(user);
    }

    /** [JSInvokable] */
    async onChordClick(chordData: string, chordIndex: number): Promise<void> {
        const _ = await this._instancesLock.lockAsync();
        try {
            for (const receiverInstance of this._instances) {
                await receiverInstance.callChordClick(chordData, chordIndex);
            }
        } finally {
            _.dispose();
        }
    }

    dispose(): void {
        this.reference.dispose();
    }

    createInstance(): ReceiverInstance {
        const instance = new ReceiverInstance(instance => {
            const _ = this._instancesLock.lock();
            try {
                const index = this._instances.indexOf(instance);
                if (index >= 0) this._instances.splice(index, 1);
            } finally {
                _.dispose();
            }
        });

        const _ = this._instancesLock.lock();
        try {
            this._instances.push(instance);
        } finally {
            _.dispose();
        }

        return instance;
    }
}
