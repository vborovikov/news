-- IIS app user to access the DB server
use master;
go

create login [IIS APPPOOL\news] from windows with default_database=[News];
go

use News;
go

create user [IIS AppPool\news] for login [IIS APPPOOL\news];
go

-- default schema [rss]
alter user [IIS AppPool\news] with default_schema=[rss]
go

-- read/write roles
alter role [db_datareader] add member [IIS AppPool\news]
go
alter role [db_datawriter] add member [IIS AppPool\news]
go

-- [asp] operations
grant insert on schema::[asp] to [IIS AppPool\news]
go
grant select on schema::[asp] to [IIS AppPool\news]
go
grant update on schema::[asp] to [IIS AppPool\news]
go
grant delete on schema::[asp] to [IIS AppPool\news]
go
grant execute on schema::[asp] to [IIS AppPool\news]
go

-- [rss] operations
grant insert on schema::[rss] to [IIS AppPool\news]
go
grant select on schema::[rss] to [IIS AppPool\news]
go
grant update on schema::[rss] to [IIS AppPool\news]
go
grant delete on schema::[rss] to [IIS AppPool\news]
go
grant execute on schema::[rss] to [IIS AppPool\news]
go
