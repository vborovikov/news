-- grant access from News db to Local Service account which is used by Newsmaker

use master;
go
create login [NT AUTHORITY\LOCAL SERVICE] from windows with default_database=[master];
go

use News;
go

create user [NT AUTHORITY\LOCAL SERVICE] for login [NT AUTHORITY\LOCAL SERVICE];
go
alter user [NT AUTHORITY\LOCAL SERVICE] with default_schema=[rss];
go

-- read/write roles
alter role [db_datareader] add member [NT AUTHORITY\LOCAL SERVICE];
go
alter role [db_datawriter] add member [NT AUTHORITY\LOCAL SERVICE];
go

-- [rss] operations
grant insert on schema::[rss] to [NT AUTHORITY\LOCAL SERVICE];
go
grant select on schema::[rss] to [NT AUTHORITY\LOCAL SERVICE];
go
grant update on schema::[rss] to [NT AUTHORITY\LOCAL SERVICE];
go
grant delete on schema::[rss] to [NT AUTHORITY\LOCAL SERVICE];
go
grant execute on schema::[rss] to [NT AUTHORITY\LOCAL SERVICE];
go

-- [dbo] operations
grant insert on schema::[dbo] to [NT AUTHORITY\LOCAL SERVICE];
go
grant select on schema::[dbo] to [NT AUTHORITY\LOCAL SERVICE];
go
grant update on schema::[dbo] to [NT AUTHORITY\LOCAL SERVICE];
go
grant delete on schema::[dbo] to [NT AUTHORITY\LOCAL SERVICE];
go
grant execute on schema::[dbo] to [NT AUTHORITY\LOCAL SERVICE];
go
