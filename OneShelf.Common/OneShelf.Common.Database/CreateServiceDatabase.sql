-- The counterpart of CreateServiceRole.sql for a service that has a database of its own (the new shelf, the music
-- service): a role, and a database of the same name owned by it and closed to the other roles. Its migrations create
-- its tables there. Run once, by hand, as the administrator:
--
--   psql -v name=<name> -v password=<password> -f CreateServiceDatabase.sql postgres

CREATE ROLE :"name" LOGIN PASSWORD :'password';
CREATE DATABASE :"name" OWNER :"name";
REVOKE ALL ON DATABASE :"name" FROM PUBLIC;
