-- grant access from News db to virtual service account which is used by Newsmaker

use master;
go
create login [NT SERVICE\Newsmaker] from windows with default_database=[News];
go

use News;
go

create user [NT SERVICE\Newsmaker] for login [NT SERVICE\Newsmaker];
go
alter user [NT SERVICE\Newsmaker] with default_schema=[rss];
go

-- read/write roles
alter role [db_datareader] add member [NT SERVICE\Newsmaker];
go
alter role [db_datawriter] add member [NT SERVICE\Newsmaker];
go

-- [rss] operations
grant insert on schema::[rss] to [NT SERVICE\Newsmaker];
go
grant select on schema::[rss] to [NT SERVICE\Newsmaker];
go
grant update on schema::[rss] to [NT SERVICE\Newsmaker];
go
grant delete on schema::[rss] to [NT SERVICE\Newsmaker];
go
grant execute on schema::[rss] to [NT SERVICE\Newsmaker];
go
