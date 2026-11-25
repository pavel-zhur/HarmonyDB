/** [Flags] */
export enum SimplificationMode {
    None = 0,
    Remove9AndMore = 1,
    Remove7 = 2 | Remove9AndMore,
    RemoveBass = 4,
    RemoveSus = 8,
    Remove6 = 16,
    All = Remove9AndMore | Remove7 | Remove6 | RemoveBass | RemoveSus,
}

/** Enum.HasFlag */
export function hasFlag(value: SimplificationMode, flag: SimplificationMode): boolean {
    return (value & flag) === flag;
}
