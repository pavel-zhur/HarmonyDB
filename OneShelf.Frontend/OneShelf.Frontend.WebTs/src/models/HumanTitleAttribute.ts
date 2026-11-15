// The original: Models/HumanTitleAttribute.cs and Models/HumanTitleExtensions.cs.
//
// [HumanTitle("...")] on an enum member is here an entry in a table of titles beside the enum:
//   export const XHumanTitles: HumanTitles<typeof X> = { [X.Member]: 'Title' };
// and value.GetHumanTitle() is getHumanTitle(X, XHumanTitles, value): the member's title, or its name when it has none.

export type HumanTitles = Readonly<Partial<Record<number, string>>>;

export function getHumanTitle(enumType: Record<string, string | number>, titles: HumanTitles, value: number): string {
    const title = titles[value];
    if (title !== undefined) return title;
    const name = enumType[value];
    return typeof name === 'string' ? name : value.toString();
}