"""The store, in the service's own PostgreSQL database. Nothing expires.

  * answers - one service's answer (JSON) to one question, by (service, kind, key, subject).
              subject '' is the shared answer, for every caller; a personal answer is kept under
              the subject the caller gave, and given back to that subject only. A purge of a
              subject is remembered, so that an answer asked before it is not kept after it.
  * blobs   - files: sound, video, pictures, and files others keep here. A blob's content is one
              version, kept in chunks of 1 MiB, so that a part of a file (an HTTP Range) is read
              without reading the whole of it. A new content is written as a new version and takes
              the old one's place only when it is whole; the version is the file's validator (ETag).
"""
from __future__ import annotations

import asyncio
import json
import uuid
from datetime import datetime
from typing import AsyncIterator

import asyncpg

import config
from errors import TOO_LARGE, ApiError

CHUNK = 1 << 20
SHARED = ""

SCHEMA = """
create table if not exists answers(
    service text not null, kind text not null, key text not null, subject text not null,
    body jsonb not null, stored timestamptz not null default now(),
    primary key (service, kind, key, subject));
create index if not exists answers_subject on answers(subject) where subject <> '';
create table if not exists subject_purges(
    subject text primary key, purged timestamptz not null);
create table if not exists blob_versions(
    version uuid primary key, mime text not null, size bigint not null default 0,
    info jsonb not null default '{}', stored timestamptz not null default now());
create table if not exists blob_chunks(
    version uuid not null references blob_versions(version) on delete cascade,
    n int not null, data bytea not null,
    primary key (version, n));
-- chunks are audio and video, compressed already: kept as they are, read in parts cheaply
alter table blob_chunks alter column data set storage external;
create table if not exists blobs(
    id text primary key, version uuid not null unique references blob_versions(version));
"""

# versions no blob points to: writes in progress, or cut short; the latter are cleared at start
CLEAR_ABANDONED = """
delete from blob_versions v where stored < now() - interval '1 day'
    and not exists (select 1 from blobs b where b.version = v.version)
"""

pool: asyncpg.Pool | None = None


async def _codecs(conn: asyncpg.Connection) -> None:
    await conn.set_type_codec("jsonb", encoder=json.dumps, decoder=json.loads, schema="pg_catalog")


async def open_pool() -> None:
    global pool
    url = config.settings().database_url
    for _ in range(30):                     # the database may still be starting
        try:
            pool = await asyncpg.create_pool(url, min_size=1, max_size=10, init=_codecs)
            break
        except (OSError, asyncpg.CannotConnectNowError):
            await asyncio.sleep(1)
    else:
        raise RuntimeError("the database does not answer")
    async with pool.acquire() as conn:
        await conn.execute(SCHEMA)
        await conn.execute(CLEAR_ABANDONED)


async def close_pool() -> None:
    if pool:
        await pool.close()


# ---------------------------------------------------------------- answers

async def get_answer(service: str, kind: str, key: str, subject: str):
    row = await pool.fetchrow(
        "select body, stored from answers where service=$1 and kind=$2 and key=$3 and subject=$4",
        service, kind, key, subject)
    return (row["body"], row["stored"]) if row else None


async def clock() -> datetime:
    """The database's time now: when an answer was asked, for put_answer."""
    return await pool.fetchval("select clock_timestamp()")


async def put_answer(service: str, kind: str, key: str, subject: str, body, asked: datetime) -> datetime | None:
    """Keeps an answer asked at `asked`, unless its subject was purged since; answers when it was
    stored, or None when it was not."""
    return await pool.fetchval(
        """insert into answers(service, kind, key, subject, body)
           select $1, $2, $3, $4, $5
           where not exists (select 1 from subject_purges where subject = $4 and purged >= $6)
           on conflict (service, kind, key, subject) do update set body = excluded.body, stored = now()
           returning stored""",
        service, kind, key, subject, body, asked)


async def purge_subject(subject: str) -> int:
    async with pool.acquire() as conn, conn.transaction():
        await conn.execute(
            """insert into subject_purges(subject, purged) values ($1, clock_timestamp())
               on conflict (subject) do update set purged = excluded.purged""", subject)
        status = await conn.execute("delete from answers where subject=$1", subject)
    return int(status.split()[-1])


# ---------------------------------------------------------------- blobs

async def blob(blob_id: str) -> dict | None:
    """A kept file: id, version, mime, size, info, stored."""
    row = await pool.fetchrow(
        """select b.id, v.version, v.mime, v.size, v.info, v.stored
           from blobs b join blob_versions v on v.version = b.version where b.id=$1""", blob_id)
    return dict(row) if row else None


async def write_blob(blob_id: str, mime: str, info: dict, pieces: AsyncIterator[bytes], limit: int | None) -> dict:
    """Keeps a file under its id. It is written as a new version; only when all of it is written
    does it take the place of what was kept there, in one transaction. A write cut short, or longer
    than `limit` bytes (when given), leaves the kept file as it was."""
    version = uuid.uuid4()
    await pool.execute("insert into blob_versions(version, mime, info) values ($1, $2, $3)", version, mime, info)
    try:
        async with pool.acquire() as conn:
            n, size, buf = 0, 0, bytearray()

            async def put(data: bytes):
                await conn.execute("insert into blob_chunks(version, n, data) values ($1, $2, $3)", version, n, data)
            async for piece in pieces:
                buf += piece
                if limit is not None and size + len(buf) > limit:
                    raise ApiError(413, TOO_LARGE, f"the file is larger than {limit} bytes")
                while len(buf) >= CHUNK:
                    await put(bytes(buf[:CHUNK]))
                    del buf[:CHUNK]
                    n, size = n + 1, size + CHUNK
            if buf:
                await put(bytes(buf))
                size += len(buf)
            async with conn.transaction():
                await conn.execute("update blob_versions set size=$2, stored=now() where version=$1", version, size)
                old = await conn.fetchval(
                    """insert into blobs(id, version) values ($1, $2)
                       on conflict (id) do update set version = excluded.version
                       returning (select version from blobs where id = $1)""", blob_id, version)
                if old:
                    await conn.execute("delete from blob_versions where version=$1", old)
    except BaseException:
        await pool.execute("delete from blob_versions where version=$1", version)
        raise
    return await blob(blob_id)


async def read_blob(version: uuid.UUID, start: int, end: int) -> AsyncIterator[bytes]:
    """The bytes start..end (inclusive) of one version of a file, chunk by chunk."""
    first, last = start // CHUNK, end // CHUNK
    for n in range(first, last + 1):
        data = await pool.fetchval("select data from blob_chunks where version=$1 and n=$2", version, n)
        if data is None:
            return
        lo = start - n * CHUNK if n == first else 0
        hi = end - n * CHUNK + 1 if n == last else len(data)
        yield data[lo:hi]


async def counts() -> dict:
    row = await pool.fetchrow(
        """select (select count(*) from answers where subject = '') shared_answers,
                  (select count(*) from answers where subject <> '') personal_answers,
                  (select count(*) from blobs) files,
                  (select coalesce(sum(v.size), 0)::bigint from blobs b join blob_versions v on v.version = b.version) bytes""")
    return dict(row)
