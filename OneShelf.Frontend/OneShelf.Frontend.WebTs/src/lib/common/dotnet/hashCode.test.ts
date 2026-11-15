import { describe, expect, it } from 'vitest';
import { stringHashCode, stringHashCodeOrdinalIgnoreCase } from './hashCode';

describe('string hash', () => {
    it('is .NET\'s Marvin with the fixed seed (values from .NET\'s own Marvin, tools/dotnet-reference)', () => {
        expect(stringHashCode('')).toBe(-1565644677);
        expect(stringHashCode('a')).toBe(2024302497);
        expect(stringHashCode('abcdefghi')).toBe(-317262641);
        expect(stringHashCode('Кино')).toBe(-1390400281);
        expect(stringHashCode('😀')).toBe(1907759585);
        expect(stringHashCode('\ud800')).toBe(135994944);
        expect(stringHashCodeOrdinalIgnoreCase('Кино')).toBe(stringHashCode('КИНО'));
    });

    it('does not collide on the strings a 31-multiplier hash does', () => {
        expect(stringHashCode('Aa')).not.toBe(stringHashCode('BB'));
        expect(stringHashCode('AaAa')).not.toBe(stringHashCode('BBBB'));
    });
});
