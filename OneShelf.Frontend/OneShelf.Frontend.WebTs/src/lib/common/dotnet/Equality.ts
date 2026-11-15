// .NET's default equality (EqualityComparer<T>.Default), expressed as a string key:
// - numbers, strings, booleans, null: by value (as float.Equals: NaN equals NaN, 0 equals -0);
// - plain objects ({ x, i }) stand for value tuples (ValueTuple): element by element. Two tuples of one C# type have
//   the same element names, so the elements are matched by name;
// - objects that implement IValueEquatable stand for records and record structs: by their members;
// - any other object (a class instance, an array or list) is by reference.

export interface IValueEquatable {
    /** A string that is equal for two values if and only if the C# record equality says they are equal. */
    equalityKey(): string;
}

const objectIds = new WeakMap<object, number>();
let nextObjectId = 1;

/** A number unique to the object for its lifetime, for reference equality inside value keys. */
export function objectId(value: object): number {
    let id = objectIds.get(value);
    if (id === undefined) {
        id = nextObjectId++;
        objectIds.set(value, id);
    }

    return id;
}

function isValueEquatable(value: object): value is IValueEquatable {
    return typeof (value as Partial<IValueEquatable>).equalityKey === 'function';
}

/** A value tuple: an object literal (its prototype is Object.prototype, or none). */
function isValueTuple(value: object): value is Record<string, unknown> {
    const prototype: unknown = Object.getPrototypeOf(value);
    return prototype === Object.prototype || prototype === null;
}

export function keyOf(value: unknown): string {
    if (value === null || value === undefined) return 'null';
    switch (typeof value) {
        case 'number':
            return Number.isNaN(value) ? 'n:NaN' : `n:${value === 0 ? 0 : value}`;
        case 'string':
            return `s:${value.length}:${value}`;
        case 'boolean':
            return value ? 'b:1' : 'b:0';
        case 'object':
            if (isValueEquatable(value)) return `r(${value.equalityKey()})`;
            if (isValueTuple(value)) return `t(${Object.keys(value).sort().map(name => `${name}=${keyOf(value[name])}`).join(',')})`;
            return `o:${objectId(value)}`;
        default:
            throw new Error(`Unsupported key type: ${typeof value}.`);
    }
}

/** EqualityComparer<T>.Default.Equals. */
export function csEquals(a: unknown, b: unknown): boolean {
    if (a === b) return true;
    return keyOf(a) === keyOf(b);
}
