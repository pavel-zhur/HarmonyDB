// The logo in the header: one of 45 pictures (five sheets of nine), the next every 15 seconds, starting at a random
// one — the old app's (Services/LogoImageSequencer), with the mock-up's positions on the sheets.

import { useEffect, useState } from 'react';

const positions: Record<number, [number, number][]> = {
    1: [[16, 25], [107, 25], [189, 25], [17, 106], [103, 106], [189, 108], [17, 192], [105, 192], [187, 192]],
    2: [[18, 25], [99, 25], [185, 25], [18, 104], [99, 104], [185, 104], [18, 184], [99, 184], [185, 184]],
    3: [[18, 25], [103, 25], [185, 25], [18, 102], [101, 102], [188, 102], [18, 180], [104, 180], [185, 180]],
    4: [[12, 25], [99, 25], [191, 25], [12, 108], [102, 108], [189, 107], [14, 191], [102, 191], [189, 191]],
    5: [[17, 29], [109, 24], [181, 26], [27, 103], [103, 104], [181, 104], [18, 182], [105, 184], [187, 184]],
};

const order = [1, 2, 3, 6, 5, 4, 7, 8, 9];
const sequence = [[1, true], [2, false], [3, true], [4, false], [5, true]]
    .flatMap(([sheet, reverse]) => (reverse ? [...order].reverse() : order).map(cell => [sheet as number, cell] as const));

function style(index: number): React.CSSProperties {
    const [sheet, cell] = sequence[index % sequence.length]!;
    const [x, y] = positions[sheet]![cell - 1]!;
    return { backgroundImage: `url(/img/l${sheet}.png)`, backgroundPosition: `-${x}px -${y}px` };
}

export function useLogo(): React.CSSProperties {
    const [index, setIndex] = useState(() => Math.floor(Math.random() * sequence.length));
    useEffect(() => {
        const timer = setInterval(() => setIndex(i => i + 1), 15_000);
        return () => clearInterval(timer);
    }, []);
    return style(index);
}
