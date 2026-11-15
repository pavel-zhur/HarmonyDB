/** A C# `out` parameter: the callee writes `value`. */
export class Out<T> {
    value: T;

    constructor(value?: T) {
        this.value = value as T;
    }
}
