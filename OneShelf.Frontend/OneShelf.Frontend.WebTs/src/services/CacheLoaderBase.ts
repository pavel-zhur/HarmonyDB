// The original: Services/CacheLoaderBase.cs.
//
// Kept as in the original: Go catches only TaskCanceledException as a cancellation (a Task.Delay or a request
// cancelled by Stop); an OperationCanceledException (ThrowIfCancellationRequested) is logged as an error and goes on as
// a failure. The log says "Chords loading ..." for the progressions loader too.

import type { ILogger } from '../framework/logging';
import { asyncVoid } from '../framework/unhandledExceptions';
import { TaskCanceledException } from '../lib/common/dotnet/exceptions';
import { AsyncDelegateEvent, DelegateEvent } from '../dotnet/events';
import { CancellationTokenSource } from '../dotnet/tasks';

export abstract class CacheLoaderBase {
    static readonly MissingChordsThresholdForProgressionsSlowFail = 30;
    static readonly MissingChordsThresholdForProgressionsOnlyFastFail = 50;
    static readonly MissingProgressionsThreshold = 20;

    protected readonly logger: ILogger;

    private _runningTokenSource: CancellationTokenSource | null = null;
    private _lastProgress: number | null | undefined = null;

    protected constructor(logger: ILogger) {
        this.logger = logger;
    }

    /** `event Func<IReadOnlyCollection<string>?, Task>? Updated` */
    readonly updated = new AsyncDelegateEvent<[availableExternalIds: ReadonlySet<string> | null]>();

    /** `event Action<int?>? ProgressChanged` */
    readonly progressChanged = new DelegateEvent<[progress: number | null]>();

    get isRunning(): boolean {
        return this._runningTokenSource !== null;
    }

    /** Usable as an event handler (`@onclick="Loader.Start"`). */
    readonly start = (): void => {
        if (this._runningTokenSource !== null) {
            return;
        }

        this._runningTokenSource = new CancellationTokenSource();
        const token = this._runningTokenSource.token;
        asyncVoid(() => this.go(token));
    };

    /** Usable as an event handler (`@onclick="Loader.Stop"`). */
    readonly stop = (): void => {
        this.onProgressChanged(null);

        if (this._runningTokenSource === null) {
            return;
        }

        this._runningTokenSource.cancel();
        this._runningTokenSource.dispose();
        this._runningTokenSource = null;
    };

    private async onUpdatedAll(availableExternalIds: ReadonlySet<string> | null): Promise<void> {
        await this.updated.invokeAll(availableExternalIds);
    }

    private onProgressChanged(progress: number | null): void {
        if (this._lastProgress === progress) return;
        this._lastProgress = progress;

        try {
            this.progressChanged.invoke(progress);
        } catch (e) {
            this.logger.logError(e, 'Progress changed invocation error.');
        }
    }

    protected async onFinished(): Promise<void> {
        await this.onUpdatedAll(null);
        this.onProgressChanged(null);
    }

    /** OnUpdated(IReadOnlyCollection<string> availableExternalIds, int progress) */
    protected async onUpdated(availableExternalIds: ReadonlySet<string>, progress: number): Promise<void> {
        await this.onUpdatedAll(availableExternalIds);
        this.onProgressChanged(progress);
    }

    dispose(): void {
        this.stop();
    }

    /** `async void Go(CancellationToken token)` */
    private async go(token: AbortSignal): Promise<void> {
        this.logger.logInformation('Chords loading started.');

        try {
            await this.work(token);
            this.onProgressChanged(null);
            this.logger.logInformation('Chords loading finished successfully.');
        } catch (e) {
            if (e instanceof TaskCanceledException) {
                this._runningTokenSource?.dispose();
                this._runningTokenSource = null;
                this.onProgressChanged(null);
                return;
            }

            this.logger.logError(e, 'Error during background cache loading.');
        }

        this._runningTokenSource?.dispose();
        this._runningTokenSource = null;

        await this.onUpdatedAll(null);
    }

    protected abstract work(token: AbortSignal): Promise<void>;
}
