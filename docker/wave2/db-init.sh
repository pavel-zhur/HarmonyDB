#!/bin/sh
# Creates the databases of this project on the shared PostgreSQL, each owned by a role of the same name, and closed to
# the other roles. Safe to run again: what exists is kept, and the roles' passwords are set anew from the environment.
set -eu

create() {
    psql -v ON_ERROR_STOP=1 -v name="$1" -v password="$2" <<'SQL'
SELECT format('CREATE ROLE %I', :'name') WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'name') \gexec
ALTER ROLE :"name" WITH LOGIN PASSWORD :'password';
SELECT format('CREATE DATABASE %I OWNER %I', :'name', :'name') WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = :'name') \gexec
REVOKE ALL ON DATABASE :"name" FROM PUBLIC;
SQL
}

create shelf "$SHELF_DB_PASSWORD"
create audio "$AUDIO_DB_PASSWORD"
