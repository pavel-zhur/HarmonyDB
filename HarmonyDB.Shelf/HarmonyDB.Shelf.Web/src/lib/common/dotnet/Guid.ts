// System.Guid, held as its "D" string (lowercase, as .NET formats and System.Text.Json writes it).

export type Guid = string;

const dFormat = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

/** Guid.NewGuid() */
export function newGuid(): Guid {
    return crypto.randomUUID();
}

/** Parses the "D" format (the only one System.Text.Json reads); null if it is not one. */
export function tryParseGuidD(value: string): Guid | null {
    return dFormat.test(value) ? value.toLowerCase() : null;
}
