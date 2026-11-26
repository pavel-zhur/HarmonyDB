// The address a person came by to an item of chords — its own link (the item's page at its source, as its header's
// `address` gives it): passed on with every reading of the item and of its media, for this page's lifetime. It is what
// opens an item to whoever holds its link where its source says so (private own chords, by their link).
//
//   cameBy(chordsId, address)   the page was opened by this address (the public page's /chords/<id>?…; the app's
//                               /chords/<id>?address=…, which the public page leads to)
//   addressOf(chordsId)         the address to pass on, if any

const addresses = new Map<string, string>();

export function cameBy(chordsId: string, address: string | null): void {
    if (address) addresses.set(chordsId, address);
}

export function addressOf(chordsId: string): string | undefined {
    return addresses.get(chordsId);
}
