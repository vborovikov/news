-- grant access from News db to virtual service account which is used by Newsmaker

use master;
go
create login [NT SERVICE\NewsmakerSvc] from windows with default_database=[News];
go

use News;
go

create user [NT SERVICE\NewsmakerSvc] for login [NT SERVICE\NewsmakerSvc];
go
alter user [NT SERVICE\NewsmakerSvc] with default_schema=[rss];
go

-- read/write roles
alter role [db_datareader] add member [NT SERVICE\NewsmakerSvc];
go
alter role [db_datawriter] add member [NT SERVICE\NewsmakerSvc];
go

-- [rss] operations
grant insert on schema::[rss] to [NT SERVICE\NewsmakerSvc];
go
grant select on schema::[rss] to [NT SERVICE\NewsmakerSvc];
go
grant update on schema::[rss] to [NT SERVICE\NewsmakerSvc];
go
grant delete on schema::[rss] to [NT SERVICE\NewsmakerSvc];
go
grant execute on schema::[rss] to [NT SERVICE\NewsmakerSvc];
go
