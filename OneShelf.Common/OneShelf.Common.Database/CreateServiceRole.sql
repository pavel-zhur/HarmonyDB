-- The PostgreSQL counterpart of CreateSeparateUser.sql: a role for a service, with a schema of its own as its search_path,
-- so that separate EF models share one database, each isolated in its schema and chosen just by the connection string.
-- The context's migrations create its tables and its migrations history in that schema.
--
--   psql -v role=<role> -v password=<password> -v schema=<schema> -f CreateServiceRole.sql <database>

CREATE ROLE :"role" LOGIN PASSWORD :'password';
CREATE SCHEMA :"schema" AUTHORIZATION :"role";
ALTER ROLE :"role" SET search_path = :"schema";
