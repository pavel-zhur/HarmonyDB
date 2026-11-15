// The original: Shared/IllustrationsView.razor.
//
// Its illustrations are IllustrationsProvider.Peek(), read when its parameters are set (OnParametersSetAsync): here
// at each render, which happens when its parent renders it.

import { Fragment } from 'react';
import { inlineEventAttribute } from '../framework/inlineEventAttribute';
import { groupBy, orderBy, compareNumbers } from '../lib/common/dotnet/Linq';
import { withIndices, withIsLast } from '../lib/common/OneShelf.Common/Extensions';
import { services } from '../services/registry';
import './IllustrationsView.scoped.scss';

export interface IllustrationsViewProps {
    songId: number | null;
    stateChanged: (state: { isOpen: boolean; isOpen2: boolean }) => void;
}

/** Enumerable.Chunk */
function chunk<T>(source: readonly T[], size: number): T[][] {
    const result: T[][] = [];
    for (let i = 0; i < source.length; i += size) {
        result.push(source.slice(i, i + size));
    }

    return result;
}

export function IllustrationsView({ songId, stateChanged }: IllustrationsViewProps) {
    const illustrationsProvider = services.illustrationsProvider;

    // OnParametersSetAsync
    const illustrations = illustrationsProvider.peek();

    const songIllustrations = songId !== null ? illustrations?.songs.get(songId) : undefined;

    return (
        <>
            <div className="page-header bg-info bg-opacity-75 mt-4 my-header my-content-addition">

                <div className="page-header-content d-block">
                    <div className="d-flex justify-content-between">
                        <div className="breadcrumb">
                            <h5 className="my-pt-25">Иллюстрации</h5>
                        </div>

                        <div className="d-flex">
                            <a className="d-flex align-items-center text-body py-2 ms-2 collapsed my-link" onClick={() => stateChanged({ isOpen: true, isOpen2: false })}>
                                <i className="ph-x-circle my-white"></i>
                            </a>
                        </div>
                    </div>

                </div>
            </div>

            {songIllustrations !== undefined && orderBy(groupBy(songIllustrations.illustrations, x => x.alterationTitle), (a, b) => compareNumbers(a.key === null ? 1 : 2, b.key === null ? 1 : 2)).map((grouping, groupingIndex) => (
                <Fragment key={groupingIndex}>
                    {grouping.key !== null && (
                        <div className="page-header bg-info bg-opacity-75 mt-4 my-header my-content-addition">

                            <div className="page-header-content d-block">
                                <div className="d-flex justify-content-between">
                                    <div className="breadcrumb">
                                        <h5 className="my-pt-25">{grouping.key ?? 'Иллюстрации'}</h5>
                                    </div>

                                    <div className="d-flex">
                                        <a className="d-flex align-items-center text-body py-2 ms-2 collapsed my-link" onClick={() => stateChanged({ isOpen: true, isOpen2: false })}>
                                            <i className="ph-x-circle my-white"></i>
                                        </a>
                                    </div>
                                </div>

                            </div>
                        </div>
                    )}

                    {chunk(grouping.items, 2).map((row, rowIndex) => (
                        <div key={rowIndex} className="row">
                            {withIsLast(withIndices(row)).map(({ x: { x: illustration, i }, isLast }) => (
                                <Fragment key={i}>
                                    <div className="col p-0">
                                        <div className="img-box">
                                            <img src={illustration.publicUrls.url256.toString()} ref={inlineEventAttribute('onclick', ` illustrationFullscreenClicked(this, '${illustration.publicUrls.url1024.toString()}') `)} />
                                            <span>{`v${illustration.version} i${illustration.i + 1} j${illustration.j + 1} #${illustration.k + 1}`}</span>
                                        </div>
                                    </div>

                                    {isLast && i === 0 && (
                                        <div className="col p-0">
                                        </div>
                                    )}
                                </Fragment>
                            ))}
                        </div>
                    ))}
                </Fragment>
            ))}
        </>
    );
}
