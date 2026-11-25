// A genre or a mood: its tracks, albums, playlists and artists; its sub-genres. And all the genres and moods of a service.

import { useTranslation } from 'react-i18next';
import { Page } from '../../app/panes';
import { useMusicCategories, useMusicCategory, type MusicCategory, type MusicService } from '../../data/music';
import { AlbumTile, ArtistTile, Back, CategoryTile, Loaded, PlaylistTile, ServiceMark, Shelf, TrackRows, type MusicPageProps } from './parts';

function findCategory(categories: readonly MusicCategory[], id: string): MusicCategory | null {
    for (const category of categories) {
        if (category.id === id) return category;
        const inside = findCategory(category.children, id);
        if (inside) return inside;
    }
    return null;
}

export function CategoryScreen({ service, id }: MusicPageProps) {
    const { t } = useTranslation();
    const page = useMusicCategory({ service, id });
    const categories = useMusicCategories(service);
    const known = findCategory(categories.data?.groups.flatMap(group => group.categories) ?? [], id);
    return (
        <Page>
            <Back />
            <div className="mu-tagband" style={known?.color ? { ['--c' as string]: known.color } : undefined}>
                <div className="kind muted small">{t('music.genreOrMood')} · <ServiceMark service={service} /></div>
                <h1>{page.data?.title ?? known?.title ?? ''}</h1>
            </div>
            {!!known?.children.length && <div className="mu-tags">{known.children.map(child => <CategoryTile key={child.id} service={service} category={child} />)}</div>}
            <Loaded query={page}>
                {data => (
                    <>
                        {data.tracks.length > 0 && <><h2>{t('music.songs')}</h2><TrackRows tracks={data.tracks} limit={12} /></>}
                        {data.playlists.length > 0 && <><h2>{t('music.playlists')}</h2><Shelf>{data.playlists.map(p => <PlaylistTile key={p.id} playlist={p} />)}</Shelf></>}
                        {data.albums.length > 0 && <><h2>{t('music.albums')}</h2><Shelf>{data.albums.map(album => <AlbumTile key={album.id} album={album} />)}</Shelf></>}
                        {data.artists.length > 0 && <><h2>{t('music.artists')}</h2><Shelf>{data.artists.map(artist => <ArtistTile key={artist.id} artist={artist} />)}</Shelf></>}
                        {!data.tracks.length && !data.playlists.length && !data.albums.length && !data.artists.length && <p className="empty">{t('music.nothing')}</p>}
                    </>
                )}
            </Loaded>
        </Page>
    );
}

export function CategoriesScreen({ service }: { service: MusicService }) {
    const { t } = useTranslation();
    const categories = useMusicCategories(service);
    return (
        <Page>
            <Back />
            <h1>{t('music.genresAndMoods')} <ServiceMark service={service} /></h1>
            <Loaded query={categories}>
                {data => data.groups.map((group, i) => (
                    <div key={group.title ?? i}>
                        {group.title && <h2>{group.title}</h2>}
                        <div className="mu-tags">{group.categories.map(category => <CategoryTile key={category.id} service={service} category={category} />)}</div>
                    </div>
                ))}
            </Loaded>
        </Page>
    );
}
