// Русский, по областям: те же файлы и ключи, что в ../en (проверяет i18n/resources.test.ts); у счётных — _one, _few,
// _many, _other.

import { common } from './common.ts';
import { nav } from './nav.ts';
import { signIn } from './signIn.ts';
import { home } from './home.ts';
import { library } from './library.ts';
import { playlists } from './playlists.ts';
import { profile } from './profile.ts';
import { song } from './song.ts';
import { music } from './music.ts';

export const ru = {
    ...common,
    ...nav,
    ...signIn,
    ...home,
    ...library,
    ...playlists,
    ...profile,
    ...song,
    ...music,
};
