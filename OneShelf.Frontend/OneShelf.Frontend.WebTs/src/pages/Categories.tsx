// The original: Pages/Categories.razor (@page "/categories").
//
// The page's fields live on an instance, as in the original; StateHasChanged re-renders. Event handlers render after
// their synchronous part and after they complete, as Blazor's do. The name inputs take their value as Blazor's
// one-way value="@x" does (src/framework/attributes.ts: blazorValue).

import { Fragment, useRef } from 'react';
import { getRequired } from '../dotnet/collections';
import { blazorChecked, blazorValue } from '../framework/attributes';
import { combineRefs, nativeChange } from '../framework/elementRefs';
import { getLogger } from '../framework/logging';
import { navigationManager } from '../framework/navigation';
import { errorHandledTask } from '../framework/unhandledExceptions';
import { useCollectionComponent } from '../hooks/useCollectionComponent';
import { useStateHasChanged } from '../hooks/useStateHasChanged';
import { MaxLikeCategoryNameLength, TotalLikeCategories } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Constants';
import { LikeCategory } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/LikeCategory';
import { LikeCategoryAccess } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/LikeCategoryAccess';
import { compareNumbers, orderBy } from '../lib/common/dotnet/Linq';
import { isNullOrWhiteSpace, trim } from '../lib/common/dotnet/text';
import { selectSingle, withIndices } from '../lib/common/OneShelf.Common/Extensions';
import type { CollectionIndex } from '../models/CollectionIndex';
import { services } from '../services/registry';

const NoLike = 88 as LikeCategoryAccess;

const logger = getLogger('OneShelf.Frontend.Web.Pages.Categories');

class CategoriesInstance {
    // @inject, in the original's order
    readonly identityProvider = services.identityProvider;
    readonly collectionIndexProvider = services.collectionIndexProvider;
    readonly api = services.api;
    readonly collectionNavigation = services.collectionNavigation;

    likes: LikeCategory[] | null = null;
    applying: string | null = null;

    readonly names: (string | null)[] = new Array<string | null>(TotalLikeCategories).fill(null);
    readonly accesses: (LikeCategoryAccess | null)[] = new Array<LikeCategoryAccess | null>(TotalLikeCategories).fill(null);
    readonly actions: ({ title: string; css: string } | null)[] = new Array<{ title: string; css: string } | null>(TotalLikeCategories).fill(null);

    constructor(private readonly stateHasChanged: () => void) {
    }

    get collectionIndex(): CollectionIndex | null {
        return this.collectionIndexProvider.peek().collectionIndex;
    }

    setName(i: number, newName: string | null): void {
        this.names[i] = selectSingle(newName === null ? null : trim(newName), x => isNullOrWhiteSpace(x) ? '' : x);
        this.applying = null;
        this.updated();
    }

    setAccess(i: number, access: LikeCategoryAccess): void {
        this.accesses[i] = access;
        this.applying = null;
        this.updated();
    }

    updated(): void {
        const collectionIndex = this.collectionIndex;
        if (collectionIndex === null) {
            this.likes = null;

            for (let i = 0; i < TotalLikeCategories; i++) {
                this.accesses[i] = null;
                this.names[i] = null;
            }

            this.stateHasChanged();
            return;
        }

        const myId = this.identityProvider.requiredIdentity.id;
        const originalLikes = orderBy(collectionIndex.collection.likeCategories, (a, b) => compareNumbers(a.order, b.order))
            .filter(c => c.userId === myId);

        // originalLikes.MaxBy(x => x.Order)?.Order
        const maxOrder = originalLikes.length === 0 ? null : originalLikes.reduce((m, x) => x.order > m.order ? x : m).order;

        const likes = withIndices([...originalLikes, ...new Array<LikeCategory | null>(TotalLikeCategories - originalLikes.length).fill(null)])
            .map(x => new LikeCategory({
                order: x.x?.order ?? (maxOrder ?? 0) + x.i + 1,
                access: this.accesses[x.i] ?? x.x?.access ?? NoLike,
                cssColor: 'any',
                cssIcon: 'any',
                name: this.names[x.i] ?? x.x?.name ?? null!,
                privateWeight: 2,
                userId: myId,
                id: x.x?.id ?? 0,
            }));

        this.likes = likes;

        for (let i = 0; i < TotalLikeCategories; i++) {
            this.actions[i] = null;

            const original = originalLikes.length > i ? originalLikes[i]! : null;
            const updated = this.likes[i]!;

            if (original === null) {
                if (updated.access === NoLike && isNullOrWhiteSpace(updated.name)) {
                    // nothing
                } else if (updated.access === NoLike) {
                    this.actions[i] = { title: '...выберите видимость...', css: 'btn-light disabled' };
                } else if (isNullOrWhiteSpace(updated.name)) {
                    this.actions[i] = { title: '...выберите имя...', css: 'btn-light disabled' };
                } else {
                    this.actions[i] = { title: 'Создать', css: 'btn-success' };
                }
            } else if (updated.access === NoLike) {
                this.actions[i] = { title: 'Удалить', css: 'btn-danger' };
            } else if (updated.name !== original.name && updated.access !== original.access) {
                this.actions[i] = { title: 'Переименовать и сменить видимость', css: 'btn-warning' };
            } else if (updated.name !== original.name) {
                this.actions[i] = { title: 'Переименовать', css: 'btn-warning' };
            } else if (updated.access !== original.access) {
                this.actions[i] = { title: 'Сменить видимость', css: 'btn-warning' };
            }
        }

        this.likes = this.likes.slice(0, originalLikes.length + 1);

        this.stateHasChanged();
    }

    revert(i: number): void {
        this.names[i] = null;
        this.accesses[i] = null;
        this.updated();
    }

    async apply(i: number): Promise<void> {
        if (this.likes === null) return;
        const like = this.likes[i]!;
        const access = this.accesses[i];

        this.applying = 'Сохраняем изменения...';
        this.stateHasChanged();
        try {
            await this.collectionIndexProvider.clear(false);

            if (access === NoLike) {
                this.applying = await this.api.deleteLikeCategory(like.id) ? 'Списочек удален успешно.' : 'Такого списочка нет.';
            } else if (like.id === 0) {
                this.applying = await this.api.createLikeCategory(like) ? 'Списочек создан.' : 'У вас уже есть 5 штук.';
            } else {
                this.applying = await this.api.updateLikeCategory(like) ? 'Изменения сохранены.' : 'Такого списочка нет.';
            }
        } catch (e) {
            logger.logError(e, 'Error applying changes.');
            this.applying = 'Не получилось :(';
        } finally {
            this.collectionIndexProvider.sync();
        }
    }

    goTo(likeCategoryId: number): void {
        if (likeCategoryId === 0) return;

        navigationManager.navigateTo(this.collectionNavigation.getExternalNavigationLink(likeCategoryId));
    }
}

export function Categories() {
    const stateHasChanged = useStateHasChanged();
    const instanceRef = useRef<CategoriesInstance | null>(null);
    const { collectionIndex, collectionIndexFailed, retryCollectionIndex } = useCollectionComponent(async () => {
        instanceRef.current!.updated();
    });

    if (instanceRef.current === null) {
        instanceRef.current = new CategoriesInstance(stateHasChanged);
        // OnInitializedAsync, before the first render.
        instanceRef.current.updated();
    }

    const page = instanceRef.current;

    // HandleEventAsync
    const on = (handler: () => void | Promise<void>) => () => {
        const result = handler();
        stateHasChanged();
        if (result instanceof Promise) void errorHandledTask(result.then(() => stateHasChanged()));
    };

    const myId = page.identityProvider.requiredIdentity.id;
    const likes = page.likes;

    const accessRadio = (i: number, like: LikeCategory, access: LikeCategoryAccess, value: string, bold: string, text: string) => (
        <label className="form-check mb-2">
            <input type="radio" className="form-check-input" name={`r-${i}`} value={value} ref={combineRefs<HTMLInputElement>(blazorChecked(like.access === access), nativeChange<HTMLInputElement>(on(() => page.setAccess(i, access))))}/>
            {'\n            '}
            <span className="form-check-label"><span className="my-bolder">{bold}</span>{text}</span>
        </label>
    );

    return (
        <>
            <h3 className={page.applying === null ? undefined : 'text-success'}>{page.applying ?? 'Списочки'}</h3>
            {'\n\n'}
            <div className="">Песни можно присоединять к спискам (на страницах песен). Поддерживается {TotalLikeCategories}.</div>
            {'\n'}
            <div className="mt-2">Это как лайки, только с индивидуальными смыслами и гибче. <span className="opacity-75">Например "люблю петь", "хочу играть", "заявки на рэпчик", и т.д.</span></div>
            {'\n'}
            <div className="mb-4 mt-2">Вы можете создать или изменить видимость своих списков здесь.</div>
            {collectionIndexFailed && page.applying === null ? (
                <>
                    <p><em>Не получилось загрузить, может нету интернета, библиотеку.</em></p>
                    {'\n    '}
                    <button className="btn btn-outline-secondary" onClick={on(retryCollectionIndex)}>Повтор</button>
                </>
            ) : collectionIndex === null || likes === null ? (
                <div className="small-loading"><img className="music-loading" src="img/music-loading.gif" title="Загрузка..." alt="Загрузка..." /></div>
            ) : (
                <>
                    <h5 className="mt-4">Видимые списки друзей</h5>
                    {'\n    '}
                    <ul>
                        {collectionIndex.collection.likeCategories.filter(c => c.userId !== myId).map((likeCategory, j) => (
                            <li key={j} className="mb-1">
                                {getRequired(collectionIndex.usersById, likeCategory.userId).title}
                                {': '}
                                {accessTitle(likeCategory.access)}
                                {'\n                '}
                                <span className="badge bg-indigo bg-opacity-10 border-start border-width-3 text-body rounded-start-0 border-indigo my-link" onClick={on(() => navigationManager.navigateTo(page.collectionNavigation.getExternalNavigationLink(likeCategory.id)))}>{likeCategory.name}</span>
                            </li>
                        ))}
                    </ul>
                    {withIndices(likes).map(({ x: like, i }) => (
                        <Fragment key={i}>
                            <h5 className="mt-4">
                                {'\n            Ваш №'}
                                {i + 1}
                                <span className={`badge bg-indigo bg-opacity-10 border-start border-width-3 text-body rounded-start-0 border-indigo ms-2 ${like.id > 0 ? 'my-link' : ''}`} onClick={on(() => page.goTo(like.id))}>{like.name}</span>
                            </h5>
                            {'\n        '}
                            <div className=" mb-3">
                                {'\n            Имя:\n            '}
                                <input type="text" maxLength={MaxLikeCategoryNameLength} className="form-control" placeholder="Дайте мне имя..." ref={blazorValue(like.name)} onInput={e => { const newName = e.currentTarget.value; on(() => page.setName(i, newName))(); }}/>
                            </div>
                            {'\n        '}
                            {accessRadio(i, like, NoLike, '', 'Нет его.', ' Список пока не создан / хотите удалить его, тыкайте.')}
                            {'\n        '}
                            {accessRadio(i, like, LikeCategoryAccess.Private, 'Private', 'Приватный.', ' Виден только вам.')}
                            {'\n        '}
                            {accessRadio(i, like, LikeCategoryAccess.SharedView, 'SharedView', 'Виден всем.', ' Другие не могут добавлять песни.')}
                            {'\n        '}
                            {accessRadio(i, like, LikeCategoryAccess.SharedEditSingle, 'SharedEditSingle', 'Меняют все, один на всех.', ' Другие могут добавлять/удалять песни.')}
                            {'\n        '}
                            {accessRadio(i, like, LikeCategoryAccess.SharedEditMulti, 'SharedEditMulti', 'Меняют все, каждый своё.', ' Другие могут добавлять песни и удалять свои добавленные, одну песню может добавить несколько человек, каждый видит кто какие добавил и может удалять из списка свои, не влияя на чужие.')}
                            {'\n        '}
                            <div className="mt-1 mb-2">
                                {page.actions[i] !== null && (
                                    <button className={`btn ${page.actions[i]!.css}`} onClick={on(() => page.apply(i))}>{page.actions[i]!.title}</button>
                                )}
                                {(page.names[i] !== null || page.accesses[i] !== null) && (
                                    <button className={`btn btn-light ${page.actions[i] !== null ? 'ms-2' : ''}`} onClick={on(() => page.revert(i))}>Отмена</button>
                                )}
                            </div>
                        </Fragment>
                    ))}
                </>
            )}
        </>
    );
}

function accessTitle(access: LikeCategoryAccess): string {
    switch (access) {
        case LikeCategoryAccess.Private: return 'Приватный';
        case LikeCategoryAccess.SharedView: return 'Виден всем';
        case LikeCategoryAccess.SharedEditMulti: return 'Меняют все';
        case LikeCategoryAccess.SharedEditSingle: return 'Меняет каждый';
        default: return '';
    }
}
