// System.Text.StringBuilder, with what the ported code uses of it.
// Environment.NewLine is "\n" in .NET running in the browser.

export const NewLine = '\n';

export class StringBuilder {
    private value = '';

    get length(): number {
        return this.value.length;
    }

    /** builder[index] */
    charAt(index: number): string {
        if (index < 0 || index >= this.value.length) throw new RangeError('Index was out of range.');
        return this.value[index]!;
    }

    /** builder[index] = c */
    setCharAt(index: number, c: string): void {
        if (index < 0 || index >= this.value.length) throw new RangeError('Index was out of range.');
        this.value = this.value.substring(0, index) + c + this.value.substring(index + 1);
    }

    append(value: string | null | undefined): this {
        if (value != null) this.value += value;
        return this;
    }

    appendLine(value?: string | null): this {
        if (value != null) this.value += value;
        this.value += NewLine;
        return this;
    }

    toString(): string {
        return this.value;
    }
}
