-- this script may be used to create separate users with separate default schemas
-- to allow separate EF models to be hosted within a single MSSQL database
-- and to separate the databases of different microservices just by providing different connection strings
-- isolating the data and ensuring they are only able to access their corresponding schemas

CREATE user [loginname] WITH PASSWORD=N'xxx'

GO

alter USER loginname
	WITH DEFAULT_SCHEMA = loginname
GO

EXEC sp_addrolemember N'db_ddladmin', N'loginname'
GO

CREATE SCHEMA loginname
AUTHORIZATION loginname

deny VIEW DEFINITION on schema :: dbo to loginname