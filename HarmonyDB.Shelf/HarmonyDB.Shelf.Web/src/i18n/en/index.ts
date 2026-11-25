// English, by area: one file per area of the app (a new area: a file here and in ../ru, and a line below).
// The keys' tree is the one every language has (i18n/resources.test.ts checks it). Plural forms: the key with _one
// and _other (Russian: _one, _few, _many, _other), called with { count }.

import { common } from './common.ts';
import { nav } from './nav.ts';
import { signIn } from './signIn.ts';
import { home } from './home.ts';
import { library } from './library.ts';
import { playlists } from './playlists.ts';
import { profile } from './profile.ts';
import { song } from './song.ts';
import { music } from './music.ts';

export const en = {
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
