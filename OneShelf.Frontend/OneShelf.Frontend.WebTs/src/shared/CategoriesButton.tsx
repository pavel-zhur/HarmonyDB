// The original: Shared/CategoriesButton.razor.
//
// Update is an EventCallback<(byte? level, int likeCategoryId)>: the owner passes a function that runs its handler as
// the owner's event. A click is this component's event too: it renders again after the sync part of the handler and
// after the handler has completed.

import { getRequired } from '../dotnet/collections';
import { stringAttributes } from '../framework/attributes';
import { errorHandledTask } from '../framework/unhandledExceptions';
import { useCollectionComponent } from '../hooks/useCollectionComponent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import type { LikeCategory } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/LikeCategory';
import { LikeCategoryAccess } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/LikeCategoryAccess';
import { compareNumbers, orderBy } from '../lib/common/dotnet/Linq';
import { services } from '../services/registry';

export interface CategoriesButtonProps {
    versionId?: number | null;
    songId: number | null;
    /** int? */
    currentTranspose?: number | null;
    update: (value: { level: number | null; likeCategoryId: number }) => Promise<void> | void;
}

export function CategoriesButton({ versionId = null, songId, currentTranspose = null, update }: CategoriesButtonProps) {
    const { collectionIndex, collectionIndexFailed } = useCollectionComponent();
    const identityProvider = services.identityProvider;
    const stateHasChanged = useStateHasChanged();

    const exists = (categoryId: number): boolean => {
        if (songId === null) return false;

        const category = getRequired(collectionIndex!.likeCategoriesById, categoryId);

        return getRequired(collectionIndex!.songsById, songId).likes.some(x =>
            x.likeCategoryId === categoryId && x.versionId === versionId
            && (category.access === LikeCategoryAccess.SharedEditSingle || x.userId === identityProvider.requiredIdentity.id)
            && x.transpose === currentTranspose);
    };

    const isSuitable = (c: LikeCategory): boolean =>
        c.access === LikeCategoryAccess.SharedEditMulti || c.access === LikeCategoryAccess.SharedEditSingle || c.userId === identityProvider.requiredIdentity.id;

    const click = async (categoryId: number) => {
        const exists_ = exists(categoryId);
        await update({ level: exists_ ? null : 0, likeCategoryId: categoryId });
    };

    // The component's own event: StateHasChanged after the sync part and after completion.
    const onClick = (categoryId: number) => {
        const task = click(categoryId);
        stateHasChanged();
        void errorHandledTask(task.then(() => stateHasChanged()));
    };

    if (collectionIndexFailed || !collectionIndex!.collection.likeCategories.some(isSuitable)) return null;

    const myId = identityProvider.requiredIdentity.id;
    const categories = orderBy(
        collectionIndex!.collection.likeCategories.filter(isSuitable),
        (a, b) => compareNumbers(a.userId === myId ? 1 : 2, b.userId === myId ? 1 : 2) || compareNumbers(a.order, b.order));

    return (
        <span className="dropdown me-1">
            <a href="#" className="my-link badge bg-indigo bg-opacity-75 border-start border-width-3 rounded-start-0 border-indigo d-inline-flex align-items-center dropdown-toggle mb-1 mt-1" data-bs-toggle="dropdown" aria-expanded="false">Списочки</a>
            {'\n\n        '}
            <div className="dropdown-menu dropdown-menu-end" ref={stringAttributes({ style: '' })}>
                {categories.map((category, i) => (
                    <a key={i}onClick={() => onClick(category.id)} className="dropdown-item my-link">
                        <span className={`border border-width-1 border-${exists(category.id) ? 'pink' : 'info'} ph-${exists(category.id) ? 'minus' : 'plus'} rounded-pill me-2`}></span>
                        {'\n                    '}
                        {category.name}
                    </a>
                ))}
            </div>
        </span>
    );
}
