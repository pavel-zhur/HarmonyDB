// The scope identifier ("b-" and ten characters) that Blazor's CSS isolation gives a component:
// the ComputeCssScope task of the .NET SDK (src/StaticWebAssetsSdk/Tasks/ScopedCss/ComputeCssScope.cs, .NET 9).
//
//   relativePath = itemSpec.ToLowerInvariant().Replace("\\", "//")
//   hash = SHA256(UTF8(relativePath + targetName))
//   "b-" + ten base-36 digits, least significant first, of |new BigInteger(hash[0..9])|
//
// The item spec is the style file's path in the project as MSBuild sees it on Windows, where the original is built
// and published (.github/workflows/frontend.yml), e.g. "Pages\Artists.razor.css"; the target name is the assembly name.

import { createHash } from 'node:crypto';

export const targetName = 'OneShelf.Frontend.Web';

export function computeCssScope(itemSpec: string): string {
    const relativePath = itemSpec.toLowerCase().split('\\').join('//');
    const hash = createHash('sha256').update(relativePath + targetName, 'utf8').digest();
    return 'b-' + toBase36(hash);
}

function toBase36(hash: Buffer): string {
    const chars = '0123456789abcdefghijklmnopqrstuvwxyz';

    // new BigInteger(byte[]): little-endian two's complement.
    let dividend = 0n;
    for (let i = 8; i >= 0; i--) {
        dividend = (dividend << 8n) | BigInt(hash[i]!);
    }

    if ((hash[8]! & 0x80) !== 0) {
        dividend -= 1n << 72n;
    }

    if (dividend < 0n) dividend = -dividend;

    let result = '';
    for (let i = 0; i < 10; i++) {
        result += chars[Number(dividend % 36n)];
        dividend /= 36n;
    }

    return result;
}
