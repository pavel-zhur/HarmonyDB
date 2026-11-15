/** CalculateHash: hash = hash * 47 + value, in unchecked int32 arithmetic. */
export function calculateHash(values: Iterable<number>): number {
    let hash = 269;
    for (const o of values) {
        hash = (Math.imul(hash, 47) + o) | 0;
    }
    return hash;
}
