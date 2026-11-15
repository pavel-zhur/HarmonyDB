// Blazor's <Virtualize Items="..."> (Microsoft.AspNetCore.Components.Web.Virtualization.Virtualize<TItem>, .NET 9,
// and its JavaScript side, Web.JS Virtualize.ts), for a fixed in-memory list of items:
//
//     <Virtualize Items="_songs" Context="item"> ... </Virtualize>
//
// becomes
//
//     <Virtualize items={songs}>{item => ...}</Virtualize>
//
// It renders a spacer element before and after the items it shows ("height: Npx; flex-shrink: 0;", as a style
// attribute set as a whole), and only the items that fit the scroll container plus the overscan. After the first render
// the spacers are watched (an IntersectionObserver on the closest scroll container with a 50px margin; a
// MutationObserver re-observes a spacer when its attributes change and gives it display: table-row inside a table; the
// scroll container gets overflow-anchor: none); when a spacer comes into view, the item size is measured from the
// distance between the spacers and the window of shown items is moved, as the original's component does, in float
// (single precision) arithmetic. Each render of the parent sets the parameters again (Items is re-read then); the
// component's own renders keep them.
//
// Not here: ItemsProvider, Placeholder, EmptyContent and RefreshDataAsync, which the site does not use; the
// element-reference marker attributes Blazor adds to the spacers (_bl_N).

import { createElement, Fragment, useEffect, useLayoutEffect, useRef, type ReactNode } from 'react';
import { flushSync } from 'react-dom';
import { useStateHasChanged } from '../hooks/useStateHasChanged';

export interface VirtualizeProps<TItem> {
    items: readonly TItem[];
    children: (item: TItem) => ReactNode;
    itemSize?: number;
    overscanCount?: number;
    spacerElement?: string;
    maxItemCount?: number;
}

/** A float's ToString(CultureInfo.InvariantCulture): the shortest text that reads back as the same float. */
function formatSingle(value: number): string {
    if (value === 0) return '0';
    for (let precision = 1; precision <= 9; precision++) {
        const text = value.toPrecision(precision);
        if (Math.fround(Number(text)) === value) return String(Number(text));
    }

    return String(value);
}

interface State {
    itemsBefore: number;
    visibleItemCapacity: number;
    unusedItemCapacity: number;
    itemCount: number;
    /** float */
    itemSize: number;
    lastRenderedItemCount: number;
    lastRenderedPlaceholderCount: number;
}

export function Virtualize<TItem>(props: VirtualizeProps<TItem>) {
    const { items, children } = props;
    const itemSizeParameter = props.itemSize ?? 50;
    const overscanCount = props.overscanCount ?? 3;
    const spacerElement = props.spacerElement ?? 'div';
    const maxItemCount = props.maxItemCount ?? 100;

    const stateHasChanged = useStateHasChanged();
    const state = useRef<State>({
        itemsBefore: 0,
        visibleItemCapacity: 0,
        unusedItemCapacity: 0,
        itemCount: 0,
        itemSize: 0,
        lastRenderedItemCount: 0,
        lastRenderedPlaceholderCount: 0,
    });
    const s = state.current;

    // OnParametersSet: the parent rendered (a new props object).
    const lastProps = useRef<VirtualizeProps<TItem> | null>(null);
    if (lastProps.current !== props) {
        lastProps.current = props;
        if (itemSizeParameter <= 0) {
            throw new Error(`Virtualize requires a positive value for parameter 'ItemSize'.`);
        }

        if (s.itemSize <= 0) {
            s.itemSize = Math.fround(itemSizeParameter);
        }

        // RefreshDataCoreAsync(renderOnSuccess: false) with the DefaultItemsProvider
        s.itemCount = items.length;
    }

    const spacerBefore = useRef<HTMLElement | null>(null);
    const spacerAfter = useRef<HTMLElement | null>(null);

    const calculateItemDistribution = (spacerSize: number, spacerSeparation: number, containerSize: number) => {
        spacerSize = Math.fround(spacerSize);
        spacerSeparation = Math.fround(spacerSeparation);
        containerSize = Math.fround(containerSize);

        if (s.lastRenderedItemCount > 0) {
            s.itemSize = Math.fround(Math.fround(spacerSeparation - Math.fround(s.lastRenderedPlaceholderCount * s.itemSize)) / s.lastRenderedItemCount);
        }

        if (s.itemSize <= 0) {
            s.itemSize = Math.fround(itemSizeParameter);
        }

        const itemsInSpacer = Math.max(0, Math.floor(Math.fround(spacerSize / s.itemSize)) - overscanCount);
        let visibleItemCapacity = Math.ceil(Math.fround(containerSize / s.itemSize)) + 2 * overscanCount;
        const unusedItemCapacity = Math.max(0, visibleItemCapacity - maxItemCount);
        visibleItemCapacity -= unusedItemCapacity;
        return { itemsInSpacer, visibleItemCapacity, unusedItemCapacity };
    };

    const updateItemDistribution = (itemsBefore: number, visibleItemCapacity: number, unusedItemCapacity: number) => {
        if (itemsBefore + visibleItemCapacity > s.itemCount) {
            itemsBefore = Math.max(0, s.itemCount - visibleItemCapacity);
        }

        if (itemsBefore !== s.itemsBefore || visibleItemCapacity !== s.visibleItemCapacity || unusedItemCapacity !== s.unusedItemCapacity) {
            s.itemsBefore = itemsBefore;
            s.visibleItemCapacity = visibleItemCapacity;
            s.unusedItemCapacity = unusedItemCapacity;
            // RefreshDataCoreAsync(renderOnSuccess: true)
            s.itemCount = lastProps.current!.items.length;
            stateHasChanged();
        }
    };

    const callbacks = useRef({
        onSpacerBeforeVisible: (_spacerSize: number, _spacerSeparation: number, _containerSize: number) => {
        },
        onSpacerAfterVisible: (_spacerSize: number, _spacerSeparation: number, _containerSize: number) => {
        },
    });

    callbacks.current.onSpacerBeforeVisible = (spacerSize, spacerSeparation, containerSize) => {
        const distribution = calculateItemDistribution(spacerSize, spacerSeparation, containerSize);
        let itemsBefore = distribution.itemsInSpacer;
        if (itemsBefore === s.itemsBefore && itemsBefore > 0) {
            itemsBefore--;
        }

        updateItemDistribution(itemsBefore, distribution.visibleItemCapacity, distribution.unusedItemCapacity);
    };

    callbacks.current.onSpacerAfterVisible = (spacerSize, spacerSeparation, containerSize) => {
        const distribution = calculateItemDistribution(spacerSize, spacerSeparation, containerSize);
        let itemsBefore = Math.max(0, s.itemCount - distribution.itemsInSpacer - distribution.visibleItemCapacity);
        if (itemsBefore === s.itemsBefore && itemsBefore < s.itemCount - distribution.visibleItemCapacity) {
            itemsBefore++;
        }

        updateItemDistribution(itemsBefore, distribution.visibleItemCapacity, distribution.unusedItemCapacity);
    };

    // BuildRenderTree
    const lastItemIndex = Math.min(s.itemsBefore + s.visibleItemCapacity, s.itemCount);
    const shown = items.slice(s.itemsBefore, Math.max(s.itemsBefore, lastItemIndex));
    s.lastRenderedItemCount = shown.length;
    s.lastRenderedPlaceholderCount = Math.max(0, lastItemIndex - s.itemsBefore - s.lastRenderedItemCount);
    const itemsAfter = Math.max(0, s.itemCount - s.visibleItemCapacity - s.itemsBefore);

    const beforeStyle = `height: ${formatSingle(Math.fround(s.itemsBefore * s.itemSize))}px; flex-shrink: 0;`;
    const afterStyle = s.unusedItemCapacity === 0
        ? `height: ${formatSingle(Math.fround(itemsAfter * s.itemSize))}px; flex-shrink: 0;`
        : `height: ${formatSingle(Math.fround(itemsAfter * s.itemSize))}px; flex-shrink: 0; transform: translateY(${formatSingle(Math.fround(s.unusedItemCapacity * s.itemSize))}px);`;

    // The style attributes, set as a whole when they change (as Blazor's renderer sets an attribute).
    const renderedStyles = useRef<{ before: string | null; after: string | null }>({ before: null, after: null });
    useLayoutEffect(() => {
        if (renderedStyles.current.before !== beforeStyle) {
            spacerBefore.current!.setAttribute('style', beforeStyle);
            renderedStyles.current.before = beforeStyle;
        }

        if (renderedStyles.current.after !== afterStyle) {
            spacerAfter.current!.setAttribute('style', afterStyle);
            renderedStyles.current.after = afterStyle;
        }
    });

    // OnAfterRenderAsync(firstRender): Virtualize.init; DisposeAsync: Virtualize.dispose
    useEffect(() => {
        const before = spacerBefore.current!;
        const after = spacerAfter.current!;
        const rootMargin = 50;

        const scrollContainer = findClosestScrollContainer(before);
        (scrollContainer ?? document.documentElement).style.overflowAnchor = 'none';

        const rangeBetweenSpacers = document.createRange();

        if (isValidTableElement(after.parentElement)) {
            before.style.display = 'table-row';
            after.style.display = 'table-row';
        }

        const intersectionObserver = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;

                rangeBetweenSpacers.setStartAfter(before);
                rangeBetweenSpacers.setEndBefore(after);
                const spacerSeparation = rangeBetweenSpacers.getBoundingClientRect().height;
                const containerSize = entry.rootBounds?.height ?? 0;

                if (entry.target === before) {
                    const spacerSize = entry.intersectionRect.top - entry.boundingClientRect.top;
                    // invokeMethodAsync: in Blazor WebAssembly the .NET method runs at once, and the render it causes is
                    // applied to the document before the call returns (the next entry measures the rows it rendered)
                    flushSync(() => callbacks.current.onSpacerBeforeVisible(spacerSize, spacerSeparation, containerSize));
                } else if (entry.target === after && after.offsetHeight > 0) {
                    const spacerSize = entry.boundingClientRect.bottom - entry.intersectionRect.bottom;
                    flushSync(() => callbacks.current.onSpacerAfterVisible(spacerSize, spacerSeparation, containerSize));
                }
            });
        }, { root: scrollContainer, rootMargin: `${rootMargin}px` });

        intersectionObserver.observe(before);
        intersectionObserver.observe(after);

        const createSpacerMutationObserver = (spacer: HTMLElement) => {
            const observerOptions = { attributes: true };
            const mutationObserver = new MutationObserver((_mutations, observer) => {
                if (isValidTableElement(spacer.parentElement)) {
                    observer.disconnect();
                    spacer.style.display = 'table-row';
                    observer.observe(spacer, observerOptions);
                }

                intersectionObserver.unobserve(spacer);
                intersectionObserver.observe(spacer);
            });

            mutationObserver.observe(spacer, observerOptions);
            return mutationObserver;
        };

        const mutationObserverBefore = createSpacerMutationObserver(before);
        const mutationObserverAfter = createSpacerMutationObserver(after);

        return () => {
            intersectionObserver.disconnect();
            mutationObserverBefore.disconnect();
            mutationObserverAfter.disconnect();
        };
    }, []);

    return (
        <>
            {createElement(spacerElement, { ref: spacerBefore })}
            {shown.map((item, i) => <Fragment key={i}>{children(item)}</Fragment>)}
            {createElement(spacerElement, { ref: spacerAfter })}
        </>
    );
}

function isValidTableElement(element: HTMLElement | null): boolean {
    if (element === null) return false;
    return ((element instanceof HTMLTableElement && element.style.display === '') || element.style.display === 'table')
        || ((element instanceof HTMLTableSectionElement && element.style.display === '') || element.style.display === 'table-row-group');
}

function findClosestScrollContainer(element: HTMLElement | null): HTMLElement | null {
    if (!element || element === document.body || element === document.documentElement) return null;
    const style = getComputedStyle(element);
    if (style.overflowY !== 'visible') return element;
    return findClosestScrollContainer(element.parentElement);
}
