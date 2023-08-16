-- IIS app user to access the DB server
use [master];
go
CREATE LOGIN [IIS APPPOOL\news] FROM WINDOWS WITH DEFAULT_DATABASE=[master];
go
use News;
go
CREATE USER [IIS AppPool\news] FOR LOGIN [IIS APPPOOL\news];
go
-- default schema [rss]
use News;
go
ALTER USER [IIS AppPool\news] WITH DEFAULT_SCHEMA=[rss]
go
-- read/write roles
use News;
go
ALTER ROLE [db_datareader] ADD MEMBER [IIS AppPool\news]
go
use News;
go
ALTER ROLE [db_datawriter] ADD MEMBER [IIS AppPool\news]
go
-- [asp] operations
use News;
go
GRANT DELETE ON SCHEMA::[asp] TO [IIS AppPool\news]
go
use News;
go
GRANT EXECUTE ON SCHEMA::[asp] TO [IIS AppPool\news]
go
use News;
go
GRANT INSERT ON SCHEMA::[asp] TO [IIS AppPool\news]
go
use News;
go
GRANT SELECT ON SCHEMA::[asp] TO [IIS AppPool\news]
go
use News;
go
GRANT UPDATE ON SCHEMA::[asp] TO [IIS AppPool\news]
go
-- [rss] operations
use News;
go
GRANT DELETE ON SCHEMA::[rss] TO [IIS AppPool\news]
go
use News;
go
GRANT EXECUTE ON SCHEMA::[rss] TO [IIS AppPool\news]
go
use News;
go
GRANT INSERT ON SCHEMA::[rss] TO [IIS AppPool\news]
go
use News;
go
GRANT SELECT ON SCHEMA::[rss] TO [IIS AppPool\news]
go
use News;
go
GRANT UPDATE ON SCHEMA::[rss] TO [IIS AppPool\news]
go
-- [dbo] operations
use News;
go
GRANT DELETE ON SCHEMA::[dbo] TO [IIS AppPool\news]
go
use News;
go
GRANT EXECUTE ON SCHEMA::[dbo] TO [IIS AppPool\news]
go
use News;
go
GRANT INSERT ON SCHEMA::[dbo] TO [IIS AppPool\news]
go
use News;
go
GRANT SELECT ON SCHEMA::[dbo] TO [IIS AppPool\news]
go
use News;
go
GRANT UPDATE ON SCHEMA::[dbo] TO [IIS AppPool\news]
go
