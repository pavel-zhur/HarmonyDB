import type { ILike } from './ILike';

export const IconRedHeart = '❤️';
export const IconYellowHeart = '💛';
export const IconOrangeHeart = '🧡';
export const IconWhiteHeart = '♡';

export const MaxLevel = 3;

export const HeartsByLevel: ReadonlyMap<number, string> = new Map([
    [0, IconWhiteHeart],
    [1, IconYellowHeart],
    [2, IconOrangeHeart],
    [3, IconRedHeart],
]);

/** GetRating(this IEnumerable<ILike>): per user the highest level, weighed 1 / 1.4 / 1.8, summed in the order users first appear. */
export function getRating(likes: Iterable<ILike>): number {
    const maxByUser = new Map<number, number>();
    for (const like of likes) {
        const current = maxByUser.get(like.userId);
        if (current === undefined || like.level > current) maxByUser.set(like.userId, like.level);
    }
    let sum = 0;
    for (const level of maxByUser.values()) {
        sum += level === 1 ? 1 : level === 2 ? 1.4 : level === 3 ? 1.8 : 0;
    }
    return sum;
}
