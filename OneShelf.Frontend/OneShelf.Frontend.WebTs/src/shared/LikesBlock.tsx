// The original: Shared/LikesBlock.razor.
//
// Update is an EventCallback<byte?>: the owner passes a function that runs its handler as the owner's event (the
// owner renders again after it, as an EventCallback makes its receiver do).

import { errorHandledTask } from '../framework/unhandledExceptions';

export interface LikesBlockProps {
    /** byte? */
    currentLevel?: number | null;
    displayDislike?: boolean;
    update: (level: number | null) => Promise<void> | void;
    level0Available: boolean;
}

export function LikesBlock({ currentLevel = null, displayDislike = true, update, level0Available }: LikesBlockProps) {
    // @onclick="async () => await Update.InvokeAsync(level)"; its task is the renderer's to handle
    const click = (level: number | null) => () => void errorHandledTask((async () => await update(level))());

    return (
        <div className="input-group ">
            {level0Available ? (
                <>
                    <div className="input-group-append">
                        <button onClick={click(0)} className={` btn btn-outline-secondary rounded-0 rounded-start ${currentLevel === 0 ? 'primary-likes' : ' '}`}><span className="fas fa-heart heart-0"></span></button>
                    </div>
                    {'\n        '}
                    <div className="input-group-append">
                        <button onClick={click(1)} className={`btn btn-outline-secondary rounded-0 ${currentLevel === 1 ? 'primary-likes' : ''}`}><span className="fas fa-heart heart-1"></span></button>
                    </div>
                </>
            ) : (
                <div className="input-group-append">
                    <button onClick={click(1)} className={` btn btn-outline-secondary rounded-0 rounded-start ${currentLevel === 1 ? 'primary-likes' : ' '}`}><span className="fas fa-heart heart-1"></span></button>
                </div>
            )}
            <div className="input-group-append">
                <button onClick={click(2)} className={`btn btn-outline-secondary rounded-0 ${currentLevel === 2 ? 'primary-likes' : ''}`}><span className="fas fa-heart heart-2"></span></button>
            </div>
            {displayDislike ? (
                <>
                    <div className="input-group-append">
                        <button onClick={click(3)} className={`btn btn-outline-secondary rounded-0 ${currentLevel === 3 ? 'primary-likes' : ''}`}><span className="fas fa-heart heart-3"></span></button>
                    </div>
                    {'\n        '}
                    <div className="input-group-append">
                        <button onClick={click(null)} className={`btn btn-outline-secondary rounded-0 rounded-end ${currentLevel === null ? 'btn primary-likes btn-likes-close' : 'btn btn-likes btn-likes-close'}`}><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 384 512"><path d="M342.6 150.6c12.5-12.5 12.5-32.8 0-45.3s-32.8-12.5-45.3 0L192 210.7 86.6 105.4c-12.5-12.5-32.8-12.5-45.3 0s-12.5 32.8 0 45.3L146.7 256 41.4 361.4c-12.5 12.5-12.5 32.8 0 45.3s32.8 12.5 45.3 0L192 301.3 297.4 406.6c12.5 12.5 32.8 12.5 45.3 0s12.5-32.8 0-45.3L237.3 256 342.6 150.6z"/></svg></button>
                    </div>
                </>
            ) : (
                <div className="input-group-append">
                    <button onClick={click(3)} className={`btn btn-outline-secondary rounded-0 rounded-end ${currentLevel === 3 ? 'primary-likes' : ''}`}><span className="fas fa-heart heart-3"></span></button>
                </div>
            )}
        </div>
    );
}
