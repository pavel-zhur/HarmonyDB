// The chords a search found: grouped by song and ordered as the old shelf did.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { Schemas } from '../../api/client';
import { foundArtists, foundOrder, foundSongs, foundSortOf } from './found.ts';

const header = (id: string, artists: string[], title: string | null): Schemas['ChordsHeader'] => ({
    id, artists, title, source: { key: 's', title: 'S' }, kinds: ['sheet'], attributes: {}, marks: { isChosen: false }, isPublic: false,
});

const found = [
    header('1', ['Кино'], 'Звезда по имени Солнце'),
    header('2', ['Beatles'], 'Let it be'),
    header('3', [' кино '], 'звезда по имени солнце'),
    header('4', ['Ёлка'], 'Прованс'),
    header('5', ['beatles'], 'Yesterday'),
    header('6', [], null),
];

test('one song for the versions of the same artists and title, in small letters, trimmed, ё as е', () => {
    const songs = foundSongs(found, 'original');
    assert.deepEqual(songs.map(x => x.versions.map(v => v.id)), [['1', '3'], ['2'], ['4'], ['5'], ['6']]);
    assert.deepEqual(songs.map(x => [x.artist, x.title]), [
        ['кино', 'звезда по имени солнце'], ['beatles', 'let it be'], ['елка', 'прованс'], ['beatles', 'yesterday'], ['n/a', 'n/a'],
    ]);
});

test('the songs in the order found, by artist, by title; the pager goes through the versions as listed', () => {
    assert.deepEqual(foundSongs(found, 'artist').map(x => x.versions[0]!.id), ['2', '5', '6', '4', '1']);
    assert.deepEqual(foundSongs(found, 'title').map(x => x.versions[0]!.id), ['2', '6', '5', '1', '4']);
    assert.deepEqual(foundOrder(found, 'original'), ['1', '3', '2', '4', '5', '6']);
    assert.deepEqual(foundOrder(found, 'artist'), ['2', '5', '6', '4', '1', '3']);
});

test('compact: each artist once with their songs — the artists in the order found, or by name with their songs by title', () => {
    assert.deepEqual(foundArtists(foundSongs(found, 'compact')).map(x => [x.artist, x.songs.map(s => s.title)]), [
        ['кино', ['звезда по имени солнце']], ['beatles', ['let it be', 'yesterday']], ['елка', ['прованс']], ['n/a', ['n/a']],
    ]);
    assert.deepEqual(foundArtists(foundSongs(found, 'compactArtist')).map(x => x.artist), ['beatles', 'n/a', 'елка', 'кино']);
    assert.deepEqual(foundOrder(found, 'compact'), ['1', '3', '2', '5', '4', '6']);
    assert.deepEqual(foundOrder(found, 'compactArtist'), ['2', '5', '6', '4', '1', '3']);
});

test('the order the address names, else the order found', () => {
    assert.equal(foundSortOf('title'), 'title');
    assert.equal(foundSortOf('compactArtist'), 'compactArtist');
    assert.equal(foundSortOf(null), 'original');
    assert.equal(foundSortOf('popular'), 'original');
});
